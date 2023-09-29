using CodeGenerator.Models;
using CodeGenerator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using CodeGenerator.Constants;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using Spectre.Console;

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
            // Prepare configuration
            var openAIConfig = new OpenAIConfig()
            {
                Model = _configuration["OpenAI:Model"]!,
                Embedding = _configuration["OpenAI:Embedding"]!,
                Key = _configuration["OpenAI:Key"]!
            };

            var jiraConfig = new JiraConfig()
            {
                Host = _configuration["Jira:Host"]!,
                Username = _configuration["Jira:Username"]!,
                Key = _configuration["Jira:Key"]!
            };

            var qdrantConfig = new QdrantConfig()
            {
                Host = _configuration["Qdrant:Host"]!,
                Key = _configuration["Qdrant:Key"]!
            };

            // Prepare kernel
            var builder = new KernelBuilder();
            builder.WithOpenAIChatCompletionService(openAIConfig.Model, openAIConfig.Key);
            builder.WithOpenAITextEmbeddingGenerationService(openAIConfig.Embedding, openAIConfig.Key);
            IKernel kernel = builder.Build();

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", qdrantConfig.Key);
            httpClient.BaseAddress = new Uri(qdrantConfig.Host);
            kernel.UseMemory(new QdrantMemoryStore(httpClient, 1536));

            var purpose = Route.MainPage();
            var userInput = new UserInput();
            switch(purpose)
            {
                case EmbeddingPurpose.ListCollection:
                    await new EmbeddingService(kernel, _configuration).ListCollection(qdrantConfig);
                    break;
                case EmbeddingPurpose.AddNewCollection:
                    await new EmbeddingService(kernel, _configuration).AddNewCollection();
                    break;
                case EmbeddingPurpose.DeleteCollection:
                    await new EmbeddingService(kernel, _configuration).DeleteCollection(qdrantConfig);
                    break;
                case GeneratePurpose.GenerateFromJira:
                    userInput = GetUserInput(GeneratePurpose.GenerateFromJira);
                    await ProjectGeneratorService.Create(userInput);
                    var requirement = await JiraService.GetJiraIssueDetails(jiraConfig, userInput);
                    await new LLMService(kernel, _configuration).GenerateFromJira(userInput, requirement);
                    break;
                case GeneratePurpose.GenerateFromAPI:
                    userInput = GetUserInput(GeneratePurpose.GenerateFromAPI);
                    await ProjectGeneratorService.Create(userInput);
                    await new LLMService(kernel, _configuration).GenerateFromAPI(userInput, qdrantConfig);
                    break;
                case GeneratePurpose.GenerateFromJiraAndAPI:
                    userInput = GetUserInput(GeneratePurpose.GenerateFromJiraAndAPI);
                    await ProjectGeneratorService.Create(userInput);
                    requirement = await JiraService.GetJiraIssueDetails(jiraConfig, userInput);
                    await new LLMService(kernel, _configuration).GenerateFromAPI(userInput, qdrantConfig, requirement);
                    break;
            }
        }

        private UserInput GetUserInput(string purpose)
        {
            var userInput = new UserInput();

            AnsiConsole.Write(new Markup($"[{Theme.Primary}]Prompt:[/]\n" + $"[{Theme.Secondary}]> [/]"));
            userInput.Prompt = Console.ReadLine()!;
            if (purpose != GeneratePurpose.GenerateFromAPI)
            {
                AnsiConsole.Write(new Markup($"\n[{Theme.Primary}]Jira issue key:[/]\n" + $"[{Theme.Secondary}]> [/]"));
                userInput.JiraIssueKey = Console.ReadLine()!;
            }
            if (purpose != GeneratePurpose.GenerateFromJira)
            {
                AnsiConsole.Write(new Markup($"\n[{Theme.Primary}]Collection Name:[/]\n" + $"[{Theme.Secondary}]> [/]"));
                userInput.CollectionName = Console.ReadLine()!;
            }
            AnsiConsole.Write(new Markup($"\n[{Theme.Primary}]Project Name:[/]\n" + $"[{Theme.Secondary}]> [/]"));
            userInput.ProjectName = Console.ReadLine()!;
            AnsiConsole.Write(new Markup($"\n[{Theme.Primary}]Project Location:[/]\n" + $"[{Theme.Secondary}]> [/]"));
            userInput.ProjectLocation = Console.ReadLine()!;

            return userInput;
        }
    }
}