using Test.Models;
using Test.Services;
using Microsoft.Extensions.Configuration;

namespace Test
{
    public class CodeGenerator
    {
        private IConfiguration _configuration;

        public CodeGenerator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task Generate()
        {
            var openAIConfig = new OpenAIConfig()
            {
                Model = _configuration["OpenAI:Model"],
                Key = _configuration["OpenAI:Key"]
            };

            Console.WriteLine("Prompt: ");
            string prompt = Console.ReadLine();
            Console.WriteLine("Provide a URL: ");
            string url = Console.ReadLine();

            var documentationContent = await WebScrapingService.GetWebContent(url);
            await LLMService.DefineProjectProperties(prompt, documentationContent, openAIConfig);
        }

    }
}