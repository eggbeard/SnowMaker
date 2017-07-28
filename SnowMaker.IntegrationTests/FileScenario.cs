using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SnowMaker.IntegrationTests
{
    [TestFixture]
    public class FileScenario : Scenarios<FileScenario.TestScope>
    {
        protected override TestScope BuildTestScope()
        {
            return new TestScope();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task<IOptimisticDataStore> BuildStoreAsync(TestScope scope)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return new DebugOnlyFileDataStore(scope.DirectoryPath);
        }

        public class TestScope : ITestScope
        {
            public TestScope()
            {
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);

                DirectoryPath = Path.Combine(Path.GetTempPath(), IdScopeName);
                Directory.CreateDirectory(DirectoryPath);
            }

            public string IdScopeName { get; private set; }
            public string DirectoryPath { get; private set; }

            public async Task<string> ReadCurrentPersistedValueAsync()
            {
                var filePath = Path.Combine(DirectoryPath, string.Format("{0}.txt", IdScopeName));
                return await FileAsyncReaderHelper.ReadAllTextAsync(filePath);
            }

            public void Dispose()
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }
        }
    }
}
