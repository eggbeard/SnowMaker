using System.Threading.Tasks;

namespace SnowMaker
{
    public interface IUniqueIdGenerator
    {
        Task<long> NextIdAsync(string scopeName);
    }
}