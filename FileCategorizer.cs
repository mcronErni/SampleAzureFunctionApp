using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionApp1
{
    public static class FileCategorizer
    {
        private static readonly Dictionary<string, List<string>> ExtensionMap = new()
    {
        { "images", new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff" } },
        { "documents", new List<string> { ".pdf", ".doc", ".docx", ".txt", ".rtf", ".odt", ".xls", ".xlsx", ".ppt", ".pptx" } },
        { "videos", new List<string> { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm" } },
        { "audio", new List<string> { ".mp3", ".wav", ".aac", ".ogg", ".flac", ".m4a" } },
        { "archives", new List<string> { ".zip", ".rar", ".7z", ".tar", ".gz" } }
    };

        public static string Categorize(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension))
                return "others";

            foreach (var (category, extensions) in ExtensionMap)
            {
                if (extensions.Contains(extension))
                    return category;
            }

            return "others";
        }
    }

}
