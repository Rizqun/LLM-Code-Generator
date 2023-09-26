using CodeGenerator.Models;
using CodeGenerator.Models.Project;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Skills.Core;
using System.Text.Json;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;
using CodeGenerator.Constants;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using System.Text;

namespace CodeGenerator.Services
{
    public static class LLMService
    {
        public static async Task GenerateCode(UserInput userInput, OpenAIConfig openAIConfig, string requirement, string purpose, QdrantConfig? qdrantConfig = null)
        {
            // Set up kernels
            var builder = new KernelBuilder();

            // Using OpenAI
            builder.WithOpenAIChatCompletionService(openAIConfig.Model, openAIConfig.Key);
            builder.WithOpenAITextEmbeddingGenerationService(openAIConfig.Embedding, openAIConfig.Key); // It is required if using qdrant

            // Build kernel
            IKernel kernel = builder.Build();

            // Import generic skill to be used
            kernel.ImportSkill(new FileIOSkill(), "file");
            kernel.ImportSkill(new AI.Plugins.CodeGenerator(kernel), nameof(AI.Plugins.CodeGenerator));

            if (purpose == Purpose.GenerateFromJira)
                await GenerateFromJira(kernel, userInput, requirement);
            else if (purpose == Purpose.GenerateFromAPI)
                await GenerateFromAPI(kernel, userInput, requirement, qdrantConfig);
        }

        public static async Task GenerateFromJira(IKernel kernel, UserInput userInput, string requirement)
        {
            // Import skill related to JIRA
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "JiraCodeGenerator");

            // Modify execute method to implement the solution based on the requirement
            var updateMethodFunction = kernel.Skills.GetFunction("JiraCodeGenerator", "UpdateMethod");
            var extractFilePathAndContentFunction = kernel.Skills.GetFunction("CodeGenerator", "ExtractFilePathAndContent");
            var fileFunction = kernel.Skills.GetFunction("file", "Write");

            var context = new ContextVariables();
            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
            context.Set("prompt", userInput.Prompt);
            context.Set("requirement", requirement);

            var result = await kernel.RunAsync(context, updateMethodFunction, extractFilePathAndContentFunction, fileFunction);

            // Update csproj file to add some package reference if needed
            var generateCsprojFunction = kernel.Skills.GetFunction("JiraCodeGenerator", "UpdateReference");
            context = new ContextVariables(result.Variables["input"]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await kernel.RunAsync(context, generateCsprojFunction, extractFilePathAndContentFunction, fileFunction);
        }

        public static async Task GenerateFromAPI(IKernel kernel, UserInput userInput, string documentationContent, QdrantConfig qdrantConfig)
        {
            // Using Qdrant
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", qdrantConfig.Key);
            httpClient.BaseAddress = new Uri(qdrantConfig.Host);
            kernel.UseMemory(new QdrantMemoryStore(httpClient, 1536));

            // Insert documentation content to memory
            await InsertDocumentationToMemory(kernel, documentationContent);

            // Import skill related to API Url
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            kernel.ImportSkill(new TextMemorySkill(kernel.Memory));
            kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "APICodeGenerator");

            // EXECUTE FUNCTION : Execute function to get API method and API endpoint URL
            var context = new ContextVariables();
            var projectProperty = new ProjectProperty();
            
            var getMethodAndEndpointFunction = kernel.Skills.GetFunction("APICodeGenerator", "GetMethodAndEndpoint");
            var cleanUpAIResponseFunction = kernel.Skills.GetFunction("CodeGenerator", "CleanUpAIResponse");
            context.Set(TextMemorySkill.CollectionParam, "api-url-documentation");
            context.Set(TextMemorySkill.RelevanceParam, "0.8");
            context.Set("prompt", userInput.Prompt);
            context.Set("question", $"What is the API method and endpoint for this request: {userInput.Prompt}?");
            var result = new SKContext();
            var success = false;
            var retry = 0;

            while (!success && retry < 5)
            {
                try
                {
                    result = await kernel.RunAsync(context, getMethodAndEndpointFunction);
                    projectProperty.ApiEndpoint = JsonSerializer.Deserialize<APIEndpoint>(result.Variables["INPUT"], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                    context.Set("appEndpoint", $"{projectProperty.ApiEndpoint.Method} {projectProperty.ApiEndpoint.Endpoint}");

                    success = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to get the API method and endpoint. Retrying...");
                    Thread.Sleep(2000);
                }
                retry++;
            }

            if (projectProperty.ApiEndpoint.NeedRequestBody == true)
            {
                var appSnippet = await GetSnippets(kernel, $"What are the request body for the following request: \n---\n{userInput.Prompt}\n---\nto hit {projectProperty.ApiEndpoint.Endpoint} using {projectProperty.ApiEndpoint.Method} method?", 3);

                // EXECUTE FUNCTION : Execute function to get the request body required when sending a request to the API
                var getRequestBodyFunction = kernel.Skills.GetFunction("APICodeGenerator", "GetRequestBodyProperty");
                context.Set("appSnippet", appSnippet);
                context.Set("fileType", FileType.JSON);

                result = await kernel.RunAsync(context, getRequestBodyFunction, cleanUpAIResponseFunction);
                context.Set("appRequestBody", result.Variables["INPUT"]);

                Console.WriteLine(context["appRequestBody"]);
                success = true;
            }
            // EXECUTE FUNCTION: Execute function to get the input of the application
            var generatePromptToGetInputFunction = kernel.Skills.GetFunction("CodeGenerator", "GeneratePromptToGetInput");
            var getApplicationInputFunction = kernel.Skills.GetFunction("APICodeGenerator", "GetApplicationInput");
            context.Set("question", $"What are the suitable application input parameters for this request: {userInput.Prompt}, to hit API URL {projectProperty.ApiEndpoint.Method} with {projectProperty.ApiEndpoint.Endpoint} method?");

            result = await kernel.RunAsync(context, generatePromptToGetInputFunction, getApplicationInputFunction);
            try
            {
                projectProperty.Input = JsonSerializer.Deserialize<Dictionary<string, string>>(result.Variables["INPUT"], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                string inputInformation = "Input parameters:";

                foreach (var dictionary in projectProperty.Input)
                {
                    string key = dictionary.Key;
                    string value = dictionary.Value;

                    inputInformation = $"{inputInformation}\n" +
                                        $"{key}: {value}";
                }

                context.Set("inputInformation", inputInformation);
            }
            catch (Exception)
            {
                context.Set("inputInformation", result.Variables["INPUT"]);
            }

            Console.WriteLine(context["inputInformation"]);

            // EXECUTE FUNCTION: Execute function to update execute method
            var updateMethodFunction = kernel.Skills.GetFunction("APICodeGenerator", "UpdateMethod");
            var fileFunction = kernel.Skills.GetFunction("file", "Write");
            context.Set("fileType", FileType.CSharp);
            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
            result = await kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);

            // EXECUTE FUNCTION: Execute function to Update csproj file to add some package reference if needed
            var generateCsprojFunction = kernel.Skills.GetFunction("APICodeGenerator", "UpdateReference");
            context.Set("fileType", FileType.XML);
            context.Set("code", result.Variables["input"][..Math.Min(1000, result.Variables["input"].Length - 1)]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await kernel.RunAsync(context, generateCsprojFunction, cleanUpAIResponseFunction, fileFunction);

            ProjectGeneratorService.NormalizeCsprojFile(Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));
        }

        private static async Task InsertDocumentationToMemory(IKernel kernel, string documentationContent)
        {
            var lines = TextChunker.SplitPlainTextLines(documentationContent, 100);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 500);

            foreach (var paragraph in paragraphs)
            {
                await kernel.Memory.SaveInformationAsync(
                    collection: "api-url-documentation",
                    text: paragraph,
                    id: Guid.NewGuid().ToString()
                );
            }
        }

        private static async Task<string> GetSnippets(IKernel kernel, string query, int limit = 1, double minRelevanceScore = 0.7)
        {
            StringBuilder sb = new StringBuilder();
            int count = 0;

            var searchResults = kernel.Memory.SearchAsync("api-url-documentation", query, limit, minRelevanceScore);

            await foreach (var searchResult in searchResults)
            {
                sb.AppendLine($"Document snippet {count}:");
                sb.AppendLine(searchResult.Metadata.Text);

                count++;
            }

            return sb.ToString();
        }
    }
}
