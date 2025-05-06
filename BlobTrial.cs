using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace FunctionApp1
{
    public class BlobTrial
    {
        private readonly ILogger<BlobTrial> _logger;

        public BlobTrial(ILogger<BlobTrial> logger)
        {
            _logger = logger;
        }

        [Function(nameof(BlobTrial))]
        public async Task Run([BlobTrigger("sample-1/uncategorized-files/{name}", Connection ="AzureWebJobsStorage")] Stream stream, string name)
        {
            using var blobStreamReader = new StreamReader(stream);
            var content = await blobStreamReader.ReadToEndAsync();
            _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");

            if (name.Equals(".placeholder")) return;

            string category = FileCategorizer.Categorize(name);

            string newFileDestination = $"{category}/{name}";

            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("sample-1");

            var getBlob = container.GetBlockBlobReference(newFileDestination);
            await getBlob.StartCopyAsync(container.GetBlockBlobReference($"uncategorized-files/{name}"));

            while(getBlob.CopyState.Status == CopyStatus.Pending)
            {
                await Task.Delay(200);
                await getBlob.FetchAttributesAsync();
            }

            var sourceBlob = container.GetBlockBlobReference($"uncategorized-files/{name}");
            await sourceBlob.DeleteIfExistsAsync();

            _logger.LogInformation($"Blob {name} moved to {newFileDestination}");

        }
    }
}
