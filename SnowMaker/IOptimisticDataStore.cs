using System.Threading.Tasks;

namespace SnowMaker
{
    public interface IOptimisticDataStore
    {
        Task<string> GetDataAsync(string blockName);
        Task<bool> TryOptimisticWriteAsync(string blockName, string data);
    }
}
