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
            // Get OpenAI information and insert to OpenAI config
            var openAIConfig = new OpenAIConfig()
            {
                Model = _configuration["OpenAI:Model"],
                Key = _configuration["OpenAI:Key"]
            };

            // Get Jira information and insert to Jira config
            var jiraConfig = new JiraConfig()
            {
                Organization = _configuration["Jira:Organization"],
                Username = _configuration["Jira:Username"],
                Key = _configuration["Jira:Key"]
            };

            var userInput = new UserInput();

            // Get user input
            Console.WriteLine("Prompt: ");
            userInput.Prompt = Console.ReadLine();
            Console.WriteLine("Jira Issue Key: ");
            userInput.JiraIssueKey = Console.ReadLine();
            Console.WriteLine("API Documentation URL: ");
            userInput.APIDocumentationURL = Console.ReadLine();
            Console.WriteLine("Project Name: ");
            userInput.ProjectName = Console.ReadLine();
            Console.WriteLine("Project Location: ");
            userInput.ProjectLocation = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput.APIDocumentationURL))
            {
                await ProjectGeneratorService.Create(userInput);
                var requirement = await JiraService.GetJiraIssueDetails(jiraConfig, userInput);

                await LLMService.GenerateCode(userInput, openAIConfig, requirement);
            }
            else
            {
                var documentationContent = await WebScrapingService.GetWebContent(userInput.APIDocumentationURL);
                await LLMService.DefineProjectProperties(userInput.Prompt, documentationContent, openAIConfig);
            }
        }

    }
}