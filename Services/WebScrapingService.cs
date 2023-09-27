using HtmlAgilityPack;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using System.Text.RegularExpressions;

namespace CodeGenerator.Services
{
    public class WebScrapingService
    {
        public static async Task<string> GetWebContent(string documentationUrls)
        {
            var urls = documentationUrls.Split(",");
            var result = string.Empty;

            foreach(var url in urls)
            {
                result += $"\n\n{ScrapeAndCleanData(url)}";
            }

            return result;
        }

        public static string ScrapeAndCleanData(string url)
        {
            // Scrape web content
            var web = new HtmlWeb();
            web.PreRequest = (request) =>
            {
                request.Timeout = 300000;
                return true;
            };
            var doc = web.Load(url);

            // Remove all unused nodes
            List<string> wordsToRemove = new List<string> { "nav", "button", "header", "footer", "sidebar", "script" };

            doc.DocumentNode.Descendants()
                .Where(node => wordsToRemove.Any(word => node.Name.ToLower().Contains(word.ToLower())))
                .ToList()
                .ForEach(node => node.Remove());

            // Get body element without all unused nodes
            var bodyElement = doc.DocumentNode.Descendants("body").FirstOrDefault();

            // Get text from selected nodes (so the html tag will not included) 
            if (bodyElement != null)
            {
                var cleanedData = new List<string>();

                var rawText = bodyElement.InnerText;
                var cleanedText = CleanData(rawText);
                cleanedData.Add(cleanedText);

                return string.Join("\n", cleanedData);
            }
            else
            {
                return "No data found.";
            }
        }

        private static string CleanData(string input)
        {
            var cleanedText = input.Trim();
            cleanedText = Regex.Replace(cleanedText, @"( )+", " ");
            cleanedText = Regex.Replace(cleanedText, @"(\n)+", "\n");
            cleanedText = Regex.Replace(cleanedText, @"(\n )+", "\n");

            return cleanedText;
        }
    }
}
