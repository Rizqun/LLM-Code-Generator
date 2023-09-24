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
                result = $"\n\n{ScrapeAndCleanData(url)}";
            }

            return result;
        }

        public static string ScrapeAndCleanData(string url)
        {
            // Scrape web content
            var web = new HtmlWeb();
            var doc = web.Load(url);

            // Get specific content related to API URL from scrapped content
            var apiUrlNodes = doc.DocumentNode.Descendants()
                            .Where(node => node.Name == "body")
                            .ToList();

            // Get text from selected nodes (so the html tag will not included) 
            if (apiUrlNodes != null)
            {
                var cleanedData = new List<string>();

                foreach (var apiUrlNode in apiUrlNodes)
                {
                    var rawText = apiUrlNode.InnerText;
                    var cleanedText = CleanData(rawText);
                    cleanedData.Add(cleanedText);
                }

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
