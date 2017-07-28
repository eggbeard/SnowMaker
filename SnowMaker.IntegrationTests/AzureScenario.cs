using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace SnowMaker.IntegrationTests
{
    [TestFixture]
    public class AzureScenario : Scenarios<AzureScenario.TestScope>
    {
        readonly CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;

        protected override TestScope BuildTestScope()
        {
            return new TestScope(CloudStorageAccount.DevelopmentStorageAccount);
        }

        protected override async Task<IOptimisticDataStore> BuildStoreAsync(TestScope scope)
        {
            return await BlobOptimisticDataStore.CreateAsync(storageAccount, scope.ContainerName);
        }

        public class TestScope : ITestScope
        {
            readonly CloudBlobClient blobClient;

            public TestScope(CloudStorageAccount account)
            {
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);
                ContainerName = string.Format("snowmakertest{0}", ticks);

                blobClient = account.CreateCloudBlobClient();
            }

            public string IdScopeName { get; private set; }
            public string ContainerName { get; private set; }

            public async Task<string> ReadCurrentPersistedValueAsync()
            {
                var blobContainer = blobClient.GetContainerReference(ContainerName);
                var blob = blobContainer.GetBlockBlobReference(IdScopeName);
                using (var stream = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(stream);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            public void Dispose()
            {
                var blobContainer = blobClient.GetContainerReference(ContainerName);
                AsyncHelper.RunSync(() => blobContainer.DeleteAsync());
            }
        }
    }
}
