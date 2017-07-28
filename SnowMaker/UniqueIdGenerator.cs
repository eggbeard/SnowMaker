using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SnowMaker
{
    public class UniqueIdGenerator : IUniqueIdGenerator
    {
        readonly IOptimisticDataStore optimisticDataStore;

        readonly IDictionary<string, ScopeState> states = new Dictionary<string, ScopeState>();
        readonly SemaphoreSlim statesSemaphore = new SemaphoreSlim(1,1);

        int batchSize = 100;
        int maxWriteAttempts = 25;

        public UniqueIdGenerator(IOptimisticDataStore optimisticDataStore)
        {
            this.optimisticDataStore = optimisticDataStore;
        }

        public int BatchSize
        {
            get { return batchSize; }
            set { batchSize = value; }
        }

        public int MaxWriteAttempts
        {
            get { return maxWriteAttempts; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("value", maxWriteAttempts, "MaxWriteAttempts must be a positive number.");

                maxWriteAttempts = value;
            }
        }

        public async Task<long> NextIdAsync(string scopeName)
        {
            var state = await GetScopeStateAsync(scopeName);
            await state.IdGenerationSemaphore.WaitAsync();
            try
            {
                if (state.LastId == state.HighestIdAvailableInBatch)
                {
                    await UpdateFromSyncStoreAsync(scopeName, state);
                }

                return Interlocked.Increment(ref state.LastId);
            }
            finally
            {
                state.IdGenerationSemaphore.Release();
            }
        }

        async Task<ScopeState> GetScopeStateAsync(string scopeName)
        {
            return await states.GetValueAsync(
                scopeName,
                statesSemaphore,
#pragma warning disable 1998
                async() =>  { return new ScopeState(); });
#pragma warning restore 1998
        }

        async Task UpdateFromSyncStoreAsync(string scopeName, ScopeState state)
        {
            var writesAttempted = 0;

            while (writesAttempted < maxWriteAttempts)
            {
                var data = await optimisticDataStore.GetDataAsync(scopeName);

                if (!long.TryParse(data, out long nextId))
                    throw new UniqueIdGenerationException(string.Format(
                       "The id seed returned from storage for scope '{0}' was corrupt, and could not be parsed as a long. The data returned was: {1}",
                       scopeName,
                       data));

                state.LastId = nextId - 1;
                state.HighestIdAvailableInBatch = nextId - 1 + batchSize;
                var firstIdInNextBatch = state.HighestIdAvailableInBatch + 1;

                var written = await optimisticDataStore.TryOptimisticWriteAsync(scopeName, firstIdInNextBatch.ToString(CultureInfo.InvariantCulture));
                if (written)
                    return;

                writesAttempted++;
            }

            throw new UniqueIdGenerationException(string.Format(
                "Failed to update the data store after {0} attempts. This likely represents too much contention against the store. Increase the batch size to a value more appropriate to your generation load.",
                writesAttempted));
        }
    }
}
