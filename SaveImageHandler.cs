using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace FunctionApp1
{
    public class SaveImageHandler
    {

        public async Task SaveImageToBlobAsync(Stream stream, string fileName, string path)
        {

            stream.Position = 0;

            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var blobClient = new BlobContainerClient(connectionString, "sample-1");
            
            var blob = blobClient.GetBlobClient($"{path}/{fileName}");
            await blob.UploadAsync(stream, overwrite: true);
        }
    }
}
