using PuppeteerSharp;
using System.Text.RegularExpressions;

namespace Test.Services
{
    public class WebScrapingService
    {
        public static async Task<string> GetWebContent(string url)
        {
            var content = string.Empty;

            await new BrowserFetcher(Product.Chrome).DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Product = Product.Chrome,
                Args = new string[] { "--no-sandbox" }
            });

            using (var page = await browser.NewPageAsync())
            {
                await page.GoToAsync(url);
                content = await page.GetContentAsync();
            }

            var result = RemoveHtmlTags(content);

            return result;
        }

        private static string RemoveHtmlTags(string input)
        {
            return Regex.Replace(input, "<.*?>", "");
        }
    }
}
