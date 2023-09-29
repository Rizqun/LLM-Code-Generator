using CodeGenerator.Models;
using CodeGenerator.Models.Project;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Skills.Core;
using System.Text.Json;
using Microsoft.SemanticKernel.Orchestration;
using CodeGenerator.Constants;
using System.Text;
using Microsoft.Extensions.Configuration;
using CodeGenerator.Helpers;
using Spectre.Console;

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
            _kernel.ImportSkill(new AI.Plugins.CodeGenerator(), nameof(AI.Plugins.CodeGenerator));
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "JiraCodeGenerator");
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "FileGenerator");

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse($"{Theme.Primary}"))
                .StartAsync(" Preparing...", async ctx =>
                {
                    // Modify execute method to implement the solution based on the requirement
                    var updateMethodFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateMethodWithJira");
                    var cleanUpAIResponseFunction = _kernel.Skills.GetFunction("CodeGenerator", "CleanUpAIResponse");
                    var fileFunction = _kernel.Skills.GetFunction("file", "Write");

                    var context = new ContextVariables();
                    context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                    context.Set("fileType", FileType.CSharp);
                    context.Set("prompt", userInput.Prompt);
                    context.Set("requirement", requirement);

                    ctx.Status(" Generating Service.cs file...");
                    var result = await _kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);
                    AnsiConsole.MarkupLine("\nService.cs successfully created!");

                    // Update csproj file to add some package reference if needed
                    var generateCsprojFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateReference");
                    context.Set("fileType", FileType.XML);
                    context.Set("code", result.Variables["input"][..Math.Min(500, result.Variables["input"].Length - 1)]);
                    context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

                    ctx.Status(" Generating Csproj file...");
                    result = await _kernel.RunAsync(context, generateCsprojFunction, cleanUpAIResponseFunction, fileFunction);
                    AnsiConsole.MarkupLine("Csproj successfully created!");

                    ctx.Status(" Normalize all generated files and create README...");
                    ProjectGeneratorService.NormalizeGeneratedFile(userInput);

                    // Update readme file
                    var generateReadmeFunction = _kernel.Skills.GetFunction("FileGenerator", "GenerateReadmeFile");
                    context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                    result = await _kernel.RunAsync(context, generateReadmeFunction);
                    context.Set("path", Path.Combine(userInput.ProjectLocation, "README.md"));
                    context.Set("content", result.Variables["INPUT"]);

                    result = await _kernel.RunAsync(context, fileFunction);
                    AnsiConsole.MarkupLine("README successfully created!");

                    // Test to run the solution
                    ctx.Status(" Build generated solution...");
                    var runResult = await CommandHelper.Execute("dotnet", $"build {userInput.ProjectLocation}");

                    if (!string.IsNullOrEmpty(runResult.error))
                    {
                        AnsiConsole.MarkupLine("Error found when trying to build the generated solution.");
                        if (runResult.error.Contains("Unable to find package") || runResult.error.Contains("Reference the package directly from the project to select a different version"))
                        {
                            // Fix reference
                            var fixReferenceFunction = _kernel.Skills.GetFunction("FileGenerator", "FixReference");
                            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));
                            context.Set("error", runResult.error);
                            context.Set("fileType", FileType.XML);

                            ctx.Status(" Fix Csproj file...");
                            result = await _kernel.RunAsync(context, fixReferenceFunction, cleanUpAIResponseFunction, fileFunction);
                            AnsiConsole.MarkupLine("The error in Csproj file has been fixed!");

                            ProjectGeneratorService.NormalizeCsprojFile(userInput);
                        }
                        else if (runResult.error.Contains("does not contain a definition"))
                        {
                            // Fix service
                            var fixServiceFunction = _kernel.Skills.GetFunction("FileGenerator", "FixService");
                            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                            context.Set("error", runResult.error);
                            context.Set("fileType", FileType.CSharp);

                            ctx.Status(" Fix Service.cs file...");
                            result = await _kernel.RunAsync(context, fixServiceFunction, cleanUpAIResponseFunction, fileFunction);
                            AnsiConsole.MarkupLine("The error in Service.cs file has been fixed!");
                        }
                    }

                    AnsiConsole.MarkupLine("Build success! your generated solution ready for a test!");
                });

            await new Route(_configuration).Back();
        }

        public async Task GenerateFromAPI(UserInput userInput, QdrantConfig qdrantConfig, string? requirement = null)
        {
            // Import required skills
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            _kernel.ImportSkill(new TextMemorySkill(_kernel.Memory));
            _kernel.ImportSkill(new FileIOSkill(), "file");
            _kernel.ImportSkill(new AI.Plugins.CodeGenerator(), nameof(AI.Plugins.CodeGenerator));
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "APICodeGenerator");
            _kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "FileGenerator");

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse($"{Theme.Primary}"))
                .StartAsync(" Preparing...", async ctx =>
                {
                    // Get API endpoint and method
                    var context = new ContextVariables();
                    var projectProperty = new ProjectProperty();

                    var getMethodAndEndpointFunction = _kernel.Skills.GetFunction("APICodeGenerator", "GetMethodAndEndpoint");
                    var cleanUpAIResponseFunction = _kernel.Skills.GetFunction("CodeGenerator", "CleanUpAIResponse");
                    var fileFunction = _kernel.Skills.GetFunction("file", "Write");
                    context.Set(TextMemorySkill.CollectionParam, userInput.CollectionName);
                    context.Set("prompt", userInput.Prompt);
                    context.Set("question", $"What is the API method and endpoint for this request: {userInput.Prompt}?");
                    var result = new SKContext();
                    var success = false;
                    var retry = 0;

                    while (!success && retry < 5)
                    {
                        try
                        {
                            ctx.Status(" Gathering information about API method and endpoint...");
                            result = await _kernel.RunAsync(context, getMethodAndEndpointFunction);
                            projectProperty.ApiEndpoint = JsonSerializer.Deserialize<APIEndpoint>(result.Variables["INPUT"], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                            context.Set("appEndpoint", $"{projectProperty.ApiEndpoint.Method} {projectProperty.ApiEndpoint.Endpoint}");

                            success = true;
                        }
                        catch (Exception)
                        {
                            AnsiConsole.MarkupLine("\nFailed to gather API method and endpoint information. Retrying");
                            Thread.Sleep(2000);
                        }
                        retry++;
                    }
                    AnsiConsole.MarkupLine("\nSuccesfully obtained API method and endpoint information!");

                    if (string.IsNullOrEmpty(requirement))
                    {
                        if (projectProperty.ApiEndpoint.NeedRequestBody == true)
                        {
                            ctx.Status(" Getting some useful snippets...");
                            var appSnippet = await GetSnippets(
                                kernel: _kernel,
                                collectionName: userInput.CollectionName,
                                query: $"What are the request body for the following request: \n---\n{userInput.Prompt}\n---\nto hit {projectProperty.ApiEndpoint.Endpoint} using {projectProperty.ApiEndpoint.Method} method?",
                                limit: 2);
                            AnsiConsole.MarkupLine("Successfully obtained the snippets!");

                            // Get request body
                            var getRequestBodyFunction = _kernel.Skills.GetFunction("APICodeGenerator", "GetRequestBodyProperty");
                            context.Set("appSnippet", appSnippet);
                            context.Set("fileType", FileType.JSON);

                            ctx.Status(" Gathering information about request body...");
                            result = await _kernel.RunAsync(context, getRequestBodyFunction, cleanUpAIResponseFunction);
                            AnsiConsole.MarkupLine("Succesfully obtained request body information!");
                            context.Set("appRequestBody", result.Variables["INPUT"]);
                        }

                        // Get application inputs
                        var generatePromptToGetInputFunction = _kernel.Skills.GetFunction("CodeGenerator", "GeneratePromptToGetInput");
                        var getApplicationInputFunction = _kernel.Skills.GetFunction("APICodeGenerator", "GetApplicationInput");
                        context.Set("question", $"What are the suitable application input parameters for this request: {userInput.Prompt}, to hit API URL {projectProperty.ApiEndpoint.Method} with {projectProperty.ApiEndpoint.Endpoint} method?");

                        ctx.Status(" Gathering information about application inputs...");
                        result = await _kernel.RunAsync(context, generatePromptToGetInputFunction, getApplicationInputFunction);
                        AnsiConsole.MarkupLine("Succesfully obtained application inputs information!");
                        context.Set("inputInformation", $"Input parameters:\"\n{result.Variables["INPUT"]}");

                        // Update service.cs
                        var updateMethodFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateMethodWithAPI");
                        context.Set("fileType", FileType.CSharp);
                        context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                        ctx.Status(" Generating Service.cs file...");
                        result = await _kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);
                        AnsiConsole.MarkupLine("Service.cs successfully created!");
                    }
                    else
                    {
                        // Update service.cs
                        var updateMethodFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateMethodWithAPIAndJira");
                        context.Set("fileType", FileType.CSharp);
                        context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                        context.Set("requirement", requirement);
                        ctx.Status(" Generating Service.cs file...");
                        result = await _kernel.RunAsync(context, updateMethodFunction, cleanUpAIResponseFunction, fileFunction);
                        AnsiConsole.MarkupLine("Service.cs successfully created!");
                    }

                    // Update reference
                    var generateCsprojFunction = _kernel.Skills.GetFunction("FileGenerator", "UpdateReference");
                    context.Set("fileType", FileType.XML);
                    context.Set("code", result.Variables["input"][..Math.Min(500, result.Variables["input"].Length - 1)]);
                    context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

                    ctx.Status(" Generating Csproj file...");
                    result = await _kernel.RunAsync(context, generateCsprojFunction, cleanUpAIResponseFunction, fileFunction);
                    AnsiConsole.MarkupLine("Csproj successfully created!");

                    ctx.Status(" Normalize all generated files and create README...");
                    ProjectGeneratorService.NormalizeGeneratedFile(userInput);

                    // Update README
                    var generateReadmeFunction = _kernel.Skills.GetFunction("FileGenerator", "GenerateReadmeFile");
                    context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                    result = await _kernel.RunAsync(context, generateReadmeFunction);
                    context.Set("path", Path.Combine(userInput.ProjectLocation, "README.md"));
                    context.Set("content", result.Variables["INPUT"]);
                    result = await _kernel.RunAsync(context, fileFunction);
                    AnsiConsole.MarkupLine("README successfully created!");

                    // Test to run the solution
                    ctx.Status(" Build generated solution...");
                    var runResult = await CommandHelper.Execute("dotnet", $"build {userInput.ProjectLocation}");

                    if (!string.IsNullOrEmpty(runResult.error))
                    {
                        AnsiConsole.MarkupLine($"[{Theme.Error}]Error[/] found when trying to build the generated solution.");
                        if (runResult.error.Contains("Unable to find package") || runResult.error.Contains("Reference the package directly from the project to select a different version"))
                        {
                            // Fix reference
                            var fixReferenceFunction = _kernel.Skills.GetFunction("FileGenerator", "FixReference");
                            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));
                            context.Set("error", runResult.error);
                            context.Set("fileType", FileType.XML);

                            ctx.Status(" Fix Csproj file...");
                            result = await _kernel.RunAsync(context, fixReferenceFunction, cleanUpAIResponseFunction, fileFunction);
                            AnsiConsole.MarkupLine($"The error in Csproj file has been [{Theme.Success}]fixed![/]");

                            ProjectGeneratorService.NormalizeCsprojFile(userInput);
                        }
                        else if (runResult.error.Contains("does not contain a definition"))
                        {
                            // Fix service
                            var fixServiceFunction = _kernel.Skills.GetFunction("FileGenerator", "FixService");
                            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
                            context.Set("error", runResult.error);
                            context.Set("fileType", FileType.CSharp);

                            ctx.Status(" Fix Service.cs file...");
                            result = await _kernel.RunAsync(context, fixServiceFunction, cleanUpAIResponseFunction, fileFunction);
                            AnsiConsole.MarkupLine("The error in Service.cs file has been fixed!");
                        }
                    }

                    AnsiConsole.MarkupLine("Build success! your generated solution ready for a test!");
                });

            await new Route(_configuration).Back();
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
