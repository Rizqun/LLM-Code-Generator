using CodeGenerator.Models;
using CodeGenerator.Services;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using CodeGenerator.Constants;

namespace CodeGenerator
{
    public class Generator
    {
        private IConfiguration _configuration;

        public Generator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task Generate()
        {
            // Get OpenAI information and insert to OpenAI config
            var openAIConfig = new OpenAIConfig()
            {
                Model = _configuration["OpenAI:Model"],
                Embedding = _configuration["OpenAI:Embedding"],
                Key = _configuration["OpenAI:Key"]
            };

            // Get Jira information and insert to Jira config
            var jiraConfig = new JiraConfig()
            {
                Organization = _configuration["Jira:Organization"],
                Username = _configuration["Jira:Username"],
                Key = _configuration["Jira:Key"]
            };

            // Get Qdrant information and insert to Qdrant config
            var qdrantConfig = new QdrantConfig()
            {
                Host = _configuration["Qdrant:Host"],
                Key = _configuration["Qdrant:Key"]
            };

            var userInput = new UserInput();

            var purpose = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                        .Title("What do you want to do?")
                        .PageSize(4)
                        .HighlightStyle("steelblue1")
                        .AddChoices(new[] {
                            Purpose.GenerateFromJira,
                            Purpose.GenerateFromAPI,
                            Purpose.GenerateFromJiraAndAPI,
                            Purpose.UpdateSolution
                        }));

            if (purpose != Purpose.UpdateSolution)
            {
                Console.WriteLine("Prompt: ");
                userInput.Prompt = Console.ReadLine();
                if (purpose != Purpose.GenerateFromAPI)
                {
                    Console.WriteLine("Jira Issue Key: ");
                    userInput.JiraIssueKey = Console.ReadLine();
                }
                if (purpose != Purpose.GenerateFromJira)
                {
                    Console.WriteLine("API Documentation URL: ");
                    userInput.APIDocumentationURL = Console.ReadLine();
                }
                Console.WriteLine("Project Name: ");
                userInput.ProjectName = Console.ReadLine();
                Console.WriteLine("Project Location: ");
                userInput.ProjectLocation = Console.ReadLine();
            }
            else
            {
                // TODO: define inputs for update solution
            }

            if (string.IsNullOrEmpty(userInput.APIDocumentationURL))
            {
                await ProjectGeneratorService.Create(userInput);
                var requirement = await JiraService.GetJiraIssueDetails(jiraConfig, userInput);
                await LLMService.GenerateCode(userInput, openAIConfig, requirement, purpose);
            }
            else
            {
                await ProjectGeneratorService.Create(userInput);
                var apiUrl = await WebScrapingService.GetWebContent(userInput.APIDocumentationURL);
                await LLMService.GenerateCode(userInput, openAIConfig, apiUrl, purpose, qdrantConfig);
            }
        }
    }
}