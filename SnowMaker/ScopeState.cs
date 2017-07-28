using System.Threading;

namespace SnowMaker
{
    class ScopeState
    {
        public readonly SemaphoreSlim IdGenerationSemaphore = new SemaphoreSlim(1,1);
        public long LastId;
        public long HighestIdAvailableInBatch;
    }
}