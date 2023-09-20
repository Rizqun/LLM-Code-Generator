using Test.Models;
using Test.Models.Project;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Skills.Core;
using System.Text.Json;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;
using Test.Constants;

namespace Test.Services
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

            string request = result["input"];

            var projectProperty = JsonSerializer.Deserialize<ProjectProperty>(request, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        }

        public static async Task GenerateCode(UserInput userInput, OpenAIConfig openAIConfig, string requirement, string purpose)
        {
            // Set up kernels
            var builder = new KernelBuilder();

            // Using OpenAI
            builder.WithOpenAIChatCompletionService(openAIConfig.Model, openAIConfig.Key);
            IKernel kernel = builder.Build();

            // Import generic skill to be used
            kernel.ImportSkill(new FileIOSkill(), "file");
            kernel.ImportSkill(new AI.Plugins.CodeGenerator(kernel), nameof(AI.Plugins.CodeGenerator));

            if (purpose == Purpose.GenerateFromJira)
                await GenerateFromJira(kernel, userInput, requirement);
            else if (purpose == Purpose.GenerateFromAPI)
                await GenerateFromAPI(kernel, userInput, requirement);
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
            context = new ContextVariables(result["input"]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await kernel.RunAsync(context, generateCsprojFunction, extractFilePathAndContentFunction, fileFunction);
        }

        public static async Task GenerateFromAPI(IKernel kernel, UserInput userInput, string apiUrl)
        {
            // Import skill related to JIRA
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "APICodeGenerator");

            // Modify execute method to implement the solution based on the requirement
            var updateMethodFunction = kernel.Skills.GetFunction("APICodeGenerator", "UpdateMethod");
            var extractFilePathAndContentFunction = kernel.Skills.GetFunction("CodeGenerator", "ExtractFilePathAndContent");
            var fileFunction = kernel.Skills.GetFunction("file", "Write");

            var context = new ContextVariables();
            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
            context.Set("prompt", userInput.Prompt);
            context.Set("apiUrl", apiUrl);

            var result = await kernel.RunAsync(context, updateMethodFunction, extractFilePathAndContentFunction, fileFunction);

            // Update csproj file to add some package reference if needed
            var generateCsprojFunction = kernel.Skills.GetFunction("APICodeGenerator", "UpdateReference");
            context = new ContextVariables(result["input"]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await kernel.RunAsync(context, generateCsprojFunction, extractFilePathAndContentFunction, fileFunction);
        }
    }
}
