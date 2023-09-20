using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Test.Services
{
    public class WebScrapingService
    {
        public static async Task<string> GetWebContent(string url)
        {
            var result = ScrapeAndCleanData(url);
            Console.WriteLine(result);

            return result;
        }

        public static string ScrapeAndCleanData(string url)
        {
            // Scrape web content
            var web = new HtmlWeb();
            var doc = web.Load(url);

            // Get specific content related to API URL from scrapped content
            var apiUrlNodes = doc.DocumentNode.Descendants()
                            .Where(node => node.Name == "code" && (node.InnerText.Contains("POST") || node.InnerText.Contains("GET") || node.InnerText.Contains("PUT") || node.InnerText.Contains("DELETE")) && node.InnerText.Contains("https"))
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

            return cleanedText;
        }
    }
}
