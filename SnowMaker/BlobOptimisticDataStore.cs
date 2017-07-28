using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        const string SeedValue = "1";

        readonly CloudBlobContainer blobContainer;

        readonly IDictionary<string, ICloudBlob> blobReferences;
        readonly SemaphoreSlim blobReferencesSemaphore = new SemaphoreSlim(1,1);

        private BlobOptimisticDataStore(CloudStorageAccount account, string containerName)
        {
            var blobClient = account.CreateCloudBlobClient();
            blobContainer = blobClient.GetContainerReference(containerName.ToLower());
            //await blobContainer.CreateIfNotExistsAsync();//can't do this in the constructor, hence the create version below

            blobReferences = new Dictionary<string, ICloudBlob>();
        }

        public static Task<BlobOptimisticDataStore> CreateAsync(CloudStorageAccount account, string containerName)
        {
            var ret = new BlobOptimisticDataStore(account, containerName);
            return ret.InitializeAsync();
        }

        private async Task<BlobOptimisticDataStore> InitializeAsync()
        {
            await blobContainer.CreateIfNotExistsAsync();
            return this;
        }

        public async Task<string> GetDataAsync(string blockName)
        {
            var blobReference = await GetBlobReferenceAsync(blockName);
            using (var stream = new MemoryStream())
            {
                await blobReference.DownloadToStreamAsync(stream);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public async Task<bool> TryOptimisticWriteAsync(string scopeName, string data)
        {
            var blobReference = await GetBlobReferenceAsync(scopeName);
            try
            {
                await UploadTextAsync(blobReference,data);
            }
            catch (StorageException exc)
            {
                if (exc.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                    return false;

                throw;
            }
            return true;
        }

        async Task<ICloudBlob> GetBlobReferenceAsync(string blockName)
        {
            return await blobReferences.GetValueAsync(
                blockName,
                blobReferencesSemaphore,
                () => InitializeBlobReference(blockName));
        }

        private async Task<ICloudBlob> InitializeBlobReference(string blockName)
        {
            var blobReference = blobContainer.GetBlockBlobReference(blockName);
            var exists = await blobReference.ExistsAsync();
            if (exists)
                return blobReference;

            try
            {
                await UploadTextAsync(blobReference, SeedValue);
            }
            catch (StorageException uploadException)
            {
                if (uploadException.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict)
                    throw;
            }

            return blobReference;
        }

        private async Task UploadTextAsync(ICloudBlob blob, string text)
        {
            blob.Properties.ContentEncoding = "UTF-8";
            blob.Properties.ContentType = "text/plain";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                await blob.UploadFromStreamAsync(stream);
            }
        }
    }
}
