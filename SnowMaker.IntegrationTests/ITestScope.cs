using System;
using System.Threading.Tasks;

namespace SnowMaker.IntegrationTests
{
    public interface ITestScope : IDisposable
    {
        string IdScopeName { get; }
        Task<string> ReadCurrentPersistedValueAsync();
    }
}