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
using CodeGenerator.Models.Response;
using Spectre.Console;
using System.Collections;

namespace CodeGenerator.Services
{
    public static class LLMService
    {
        public static async Task DefineProjectProperties(string prompt, string documentationContent, OpenAIConfig openAIConfig)
        {
            // Set up kernels
            var builder = new KernelBuilder();

            // Using OpenAI
            builder.WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", openAIConfig.Key);
            builder.WithOpenAIChatCompletionService(openAIConfig.Model, openAIConfig.Key);
            builder.WithMemoryStorage(new VolatileMemoryStore());
            IKernel kernel = builder.Build();

            var lines = TextChunker.SplitPlainTextLines(documentationContent, 30);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 100);

            foreach( var paragraph in paragraphs )
            {
                await kernel.Memory.SaveInformationAsync(
                    collection: "Documentation",
                    text: paragraph,
                    id: Guid.NewGuid().ToString()
                );
            }

            var context = kernel.CreateNewContext();
            context.Variables.Set("input", prompt);
            context.Variables[TextMemorySkill.CollectionParam] = "Documentation";

            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");

            // Import TextMemorySkill to the kernel so we can use 'recall' function in the skprompt.txt file
            // recall is used so it can h
            kernel.ImportSkill(new TextMemorySkill(kernel.Memory));
            kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "CodeGenerator");

            var result = await kernel.RunAsync(context.Variables, kernel.Skills.GetFunction("CodeGenerator", "GenerateCode"));

            string request = result.Variables["input"];

            var projectProperty = JsonSerializer.Deserialize<ProjectProperty>(request, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        }

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

            // EXECUTE FUNCTION : Execute function to get API method and endpoint URL
            var context = new ContextVariables();
            var projectProperty = new ProjectProperty();
            
            var getMethodAndEndpointFunction = kernel.Skills.GetFunction("APICodeGenerator", "GetMethodAndEndpoint");
            context.Set(TextMemorySkill.CollectionParam, "api-url-documentation");
            context.Set(TextMemorySkill.RelevanceParam, "0.8");
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

                    success = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to get the API method and endpoint. Retrying...");
                }
                retry++;
            }

            // EXECUTE FUNCTION : Execute function to get the authentication method
            var getAuthenticationMethodFunction = kernel.Skills.GetFunction("APICodeGenerator", "GetAuthenticationMethod");
            context.Set("question", $"What is the best authentication method for this request: {userInput.Prompt}, to hit API URL {projectProperty.ApiEndpoint.Endpoint} using {projectProperty.ApiEndpoint.Method} method?");
            success = false;
            retry = 0;
            while (!success && retry < 5)
            {
                try
                {
                    result = await kernel.RunAsync(context, getAuthenticationMethodFunction);

                    if (result.Variables["INPUT"] == AuthenticationMethod.OAuth || result.Variables["INPUT"] == AuthenticationMethod.APIKey || result.Variables["INPUT"] == AuthenticationMethod.BearerToken || result.Variables["INPUT"] == AuthenticationMethod.BasicAuth)
                    {
                        projectProperty.ApiEndpoint.AuthenticationMethod = result.Variables["INPUT"];
                        success = true;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to get the authentication method. Retrying...");
                }
                retry++;
            }

            // EXECUTE FUNCTION : Execute function to get the request body required when sending a request to the API
            if (projectProperty.ApiEndpoint.NeedRequestBody == true)
            {
                var getRequestBodyFunction = kernel.Skills.GetFunction("APICodeGenerator", "GetRequestBodyProperty");
                context.Set("question", $"What are the request body for this request: {userInput.Prompt}, using the {projectProperty.ApiEndpoint.Method} method to hit {projectProperty.ApiEndpoint.Endpoint}?");

                result = await kernel.RunAsync(context, getRequestBodyFunction);
            }

            // TESTING: Answer question
            //var memoryList = new List<MemoryQueryResult>();
            //var searchResults = kernel.Memory.SearchAsync("api-url-documentation", userInput.Prompt, 100);

            //await foreach(var searchResult in searchResults)
            //{
            //    memoryList.Add(searchResult);
            //}

            //memoryList = memoryList.OrderByDescending(m => m.Relevance).ToList();

            // Modify execute method to implement the solution based on the requirement
            //var updateMethodFunction = kernel.Skills.GetFunction("APICodeGenerator", "UpdateMethod");
            //var extractFilePathAndContentFunction = kernel.Skills.GetFunction("CodeGenerator", "ExtractFilePathAndContent");
            //var fileFunction = kernel.Skills.GetFunction("file", "Write");

            //context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
            context.Set("prompt", userInput.Prompt);
            //context.Set("apiUrl", apiUrl);

            //var result = await kernel.RunAsync(context, answerQuestionFunction);
            //var result = await kernel.RunAsync(context, updateMethodFunction, extractFilePathAndContentFunction, fileFunction);

            // Update csproj file to add some package reference if needed
            var generateCsprojFunction = kernel.Skills.GetFunction("APICodeGenerator", "UpdateReference");
            //context = new ContextVariables(result.Variables["input"]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            //result = await kernel.RunAsync(context, generateCsprojFunction, extractFilePathAndContentFunction, fileFunction);
        }

        private static async Task InsertDocumentationToMemory(IKernel kernel, string documentationContent)
        {
            var lines = TextChunker.SplitPlainTextLines(documentationContent, 100);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 800);

            foreach (var paragraph in paragraphs)
            {
                await kernel.Memory.SaveInformationAsync(
                    collection: "api-url-documentation",
                    text: paragraph,
                    id: Guid.NewGuid().ToString()
                );
            }
        }
    }
}
