using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionApp1
{
    public class ImageProcessor
    {
        private readonly ILogger<ImageProcessor> _logger;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        public ImageProcessor(ILogger<ImageProcessor> logger)
        {
            _logger = logger;
        }

        [Function("ImageProcessor")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-image")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            if(!req.HasFormContentType || !req.ContentType.StartsWith("multipart/form-data"))
            {
                return new BadRequestObjectResult("Invalid Content-Type");
            }
            var formData = await req.ReadFormAsync();
            var file = formData.Files.GetFile("image");
            if (file == null)
            {
                return new BadRequestObjectResult("No file found in the request.");
            }

            string mimeType = file.ContentType;
            string displayName = file.FileName;

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            MemoryStream stream = new MemoryStream();
            await file.CopyToAsync(stream);


            int contentLength = imageBytes.Length;

            var startRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={googleApiKey}");
            startRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
            startRequest.Headers.Add("X-Goog-Upload-Command", "start");
            startRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", contentLength.ToString());
            startRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
            startRequest.Content = new StringContent($"{{\"file\": {{\"display_name\": \"{displayName}\"}}}}", Encoding.UTF8, "application/json");

            var startResponse = await _httpClient.SendAsync(startRequest);
            if(!startResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var uploadUrlValues))
            {
                return new StatusCodeResult(500);
            }
            string uploadUrl = uploadUrlValues.First();

            var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            uploadRequest.Content = new ByteArrayContent(imageBytes);
            uploadRequest.Content.Headers.ContentLength = contentLength;
            uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
            uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");

            var uploadResponse = await _httpClient.SendAsync(uploadRequest);
            var uploadJson = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
            string fileUri = uploadJson.RootElement.GetProperty("file").GetProperty("uri").GetString();


            var prompt = @"Generate a caption for this image. Then classify the image based on its feature. Only one classification.
                
                            Current classifications:
                            images/Food
                            images/Plants
                            images/Landmarks
                            images/Technology
                            images/Animals
                            images/Others

                            Create a classification if it doesn't exist. Use the same format images/classification-name
                            Use this JSON schema:

                            result = {'image-caption': str, 'image-classification': str}
                            Return: result";


            var genPayload = new
            {
                contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new {
                            file_data = new {
                                mime_type = mimeType,
                                file_uri = fileUri
                            }
                        },
                        new { text = prompt }
                    }
                }
            }
            };

            string jsonPayload = JsonSerializer.Serialize(genPayload);
            var genRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={googleApiKey}");
            genRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var genResponse = await _httpClient.SendAsync(genRequest);
            string genResponseText = await genResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("Gemini Response: " + genResponseText);

            // Optional: extract only the generated JSON string from Gemini response
            var genDoc = JsonDocument.Parse(genResponseText);
            _logger.LogInformation("Gen Doc: " + genDoc);


            var text = genDoc.RootElement
                             .GetProperty("candidates")[0]
                             .GetProperty("content")
                             .GetProperty("parts")[0]
                             .GetProperty("text")
                             .GetString();

            _logger.LogInformation("Text: " + text);

            //string pattern = @"\{.*\}";
            //Match match = Regex.Match(text, pattern);
            //text = match.Value;
            text = Regex.Replace(text, @"^```json\s*|```$", "", RegexOptions.Multiline).Trim();
            _logger.LogInformation("Text: " + text);
            var parsedText = JsonDocument.Parse(text).RootElement;

            SaveImageHandler obj = new SaveImageHandler();
            obj.SaveImageToBlobAsync(stream, displayName, parsedText.GetProperty("image-classification").GetString());


            return new OkObjectResult(parsedText);
        }
    }
}
