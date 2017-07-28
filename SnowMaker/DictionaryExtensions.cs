using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SnowMaker
{
    public static class DictionaryExtensions
    {
        public static async Task<TValue> GetValueAsync<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            SemaphoreSlim dictionarySemaphoreSlim,
            Func<Task<TValue>> valueInitializer)
        {
            TValue value;
            var found = dictionary.TryGetValue(key, out value);
            if (found) return value;

            await dictionarySemaphoreSlim.WaitAsync();
            try
            {
                found = dictionary.TryGetValue(key, out value);
                if (found) return value;

                value = await valueInitializer();

                dictionary.Add(key, value);
            }
            finally
            {
                dictionarySemaphoreSlim.Release();
            }
            return value;
        }
    }
}