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
using Microsoft.Extensions.Configuration;

namespace CodeGenerator.Services
{
    public class LLMService
    {
        private IKernel _kernel;
        private IConfiguration _configuration;

        public LLMService(IKernel kernel, IConfiguration configuration)
        {
            _kernel = kernel;
            _configuration = configuration;
        }

        public async Task GenerateFromJira(UserInput userInput, string requirement)
        {
            // Import required skills
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            _kernel.ImportSkill(new FileIOSkill(), "file");
            _kernel.ImportSkill(new AI.Plugins.CodeGenerator(_kernel), nameof(AI.Plugins.CodeGenerator));
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "JiraCodeGenerator");
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "FileGenerator");

            // Modify execute method to implement the solution based on the requirement
            var updateMethodFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateMethodWithJira");
            var cleanUpAIResponseFunction = _kernel.Skills.GetFunction("CodeGenerator", "CleanUpAIResponse");
            var fileFunction = _kernel.Skills.GetFunction("file", "Write");

            var context = new ContextVariables();
            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
            context.Set("fileType", FileType.CSharp);
            context.Set("prompt", userInput.Prompt);
            context.Set("requirement", requirement);

            var result = await _kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);

            // Update csproj file to add some package reference if needed
            var generateCsprojFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateReference");
            context.Set("fileType", FileType.XML);
            context.Set("code", result.Variables["input"][..Math.Min(500, result.Variables["input"].Length - 1)]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await _kernel.RunAsync(context, generateCsprojFunction, cleanUpAIResponseFunction, fileFunction);

            ProjectGeneratorService.NormalizeCsprojFile(Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));
        }

        public async Task GenerateFromAPI(UserInput userInput, QdrantConfig qdrantConfig, string? requirement = null)
        {
            // Import required skills
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            _kernel.ImportSkill(new TextMemorySkill(_kernel.Memory));
            _kernel.ImportSkill(new FileIOSkill(), "file");
            _kernel.ImportSkill(new AI.Plugins.CodeGenerator(_kernel), nameof(AI.Plugins.CodeGenerator));
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "APICodeGenerator");
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "FileGenerator");

            // EXECUTE FUNCTION : Execute function to get API method and API endpoint URL
            var context = new ContextVariables();
            var projectProperty = new ProjectProperty();
            
            var getMethodAndEndpointFunction = _kernel.Skills.GetFunction("APICodeGenerator", "GetMethodAndEndpoint");
            var cleanUpAIResponseFunction = _kernel.Skills.GetFunction("CodeGenerator", "CleanUpAIResponse");
            var fileFunction = _kernel.Skills.GetFunction("file", "Write");
            context.Set(TextMemorySkill.CollectionParam, "api-url-documentation");
            context.Set("prompt", userInput.Prompt);
            context.Set("question", $"What is the API method and endpoint for this request: {userInput.Prompt}?");
            var result = new SKContext();
            var success = false;
            var retry = 0;

            while (!success && retry < 5)
            {
                try
                {
                    result = await _kernel.RunAsync(context, getMethodAndEndpointFunction);
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

            if (string.IsNullOrEmpty(requirement)) 
            {
                if (projectProperty.ApiEndpoint.NeedRequestBody == true)
                {
                    var appSnippet = await GetSnippets(
                        kernel: _kernel,
                        collectionName: userInput.CollectionName,
                        query: $"What are the request body for the following request: \n---\n{userInput.Prompt}\n---\nto hit {projectProperty.ApiEndpoint.Endpoint} using {projectProperty.ApiEndpoint.Method} method?",
                        limit: 3);

                    // EXECUTE FUNCTION : Execute function to get the request body required when sending a request to the API
                    var getRequestBodyFunction = _kernel.Skills.GetFunction("APICodeGenerator", "GetRequestBodyProperty");
                    context.Set("appSnippet", appSnippet);
                    context.Set("fileType", FileType.JSON);

                    result = await _kernel.RunAsync(context, getRequestBodyFunction, cleanUpAIResponseFunction);
                    context.Set("appRequestBody", result.Variables["INPUT"]);

                    Console.WriteLine(context["appRequestBody"]);
                }

                // EXECUTE FUNCTION: Execute function to get the input of the application
                var generatePromptToGetInputFunction = _kernel.Skills.GetFunction("CodeGenerator", "GeneratePromptToGetInput");
                var getApplicationInputFunction = _kernel.Skills.GetFunction("APICodeGenerator", "GetApplicationInput");
                context.Set("question", $"What are the suitable application input parameters for this request: {userInput.Prompt}, to hit API URL {projectProperty.ApiEndpoint.Method} with {projectProperty.ApiEndpoint.Endpoint} method?");

                result = await _kernel.RunAsync(context, generatePromptToGetInputFunction, getApplicationInputFunction);
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
                var updateMethodFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateMethodWithAPI");
                context.Set("fileType", FileType.CSharp);
                context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                result = await _kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);
            }
            else
            {
                // EXECUTE FUNCTION: Execute function to update execute method
                var updateMethodFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateMethodWithAPIAndJira");
                context.Set("fileType", FileType.CSharp);
                context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                result = await _kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);
            }

            // EXECUTE FUNCTION: Execute function to Update csproj file to add some package reference if needed
            var generateCsprojFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateReference");
            context.Set("fileType", FileType.XML);
            context.Set("code", result.Variables["input"][..Math.Min(500, result.Variables["input"].Length - 1)]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await _kernel.RunAsync(context, generateCsprojFunction, cleanUpAIResponseFunction, fileFunction);

            ProjectGeneratorService.NormalizeCsprojFile(Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));
        }

        private static async Task<string> GetSnippets(IKernel kernel, string collectionName, string query, int limit = 1, double minRelevanceScore = 0.7)
        {
            StringBuilder sb = new StringBuilder();
            int count = 1;

            var searchResults = kernel.Memory.SearchAsync(collectionName, query, limit, minRelevanceScore);

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
