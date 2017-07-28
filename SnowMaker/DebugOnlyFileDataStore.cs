using System.IO;
using System.Threading.Tasks;

namespace SnowMaker
{
    public class DebugOnlyFileDataStore : IOptimisticDataStore
    {
        const string SeedValue = "1";

        readonly string directoryPath;

        public DebugOnlyFileDataStore(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public async Task<string> GetDataAsync(string blockName)
        {
            var blockPath = Path.Combine(directoryPath, string.Format("{0}.txt", blockName));
            try
            {
                return File.ReadAllText(blockPath);
            }
            catch (FileNotFoundException)
            {
                using (var file = File.Create(blockPath))
                using (var streamWriter = new StreamWriter(file))
                {
                    await streamWriter.WriteAsync(SeedValue);
                }
                return SeedValue;
            }
        }

#pragma warning disable 1998
        public async Task<bool> TryOptimisticWriteAsync(string blockName, string data)
#pragma warning restore 1998
        {
            var blockPath = Path.Combine(directoryPath, string.Format("{0}.txt", blockName));
            File.WriteAllText(blockPath, data);
            return true;
        }
    }
}
