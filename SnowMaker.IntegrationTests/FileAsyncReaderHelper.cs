using System;
using System.IO;
using System.Threading.Tasks;

namespace SnowMaker.IntegrationTests
{
    public class FileAsyncReaderHelper
    {
        public static async Task<String> ReadAllTextAsync(String path)
        {
            String result;
            using (StreamReader reader = File.OpenText(path))
            {
                result = await reader.ReadToEndAsync();
            }
            return result;
        }
    }
}
