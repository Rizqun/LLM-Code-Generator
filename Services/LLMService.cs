using Test.Models;
using Test.Models.Project;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Skills.Core;
using System.Text.Json;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;

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
            kernel.ImportSkill(new TextMemorySkill(kernel.Memory));
            kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "CodeGenerator");

            var result = await kernel.RunAsync(context.Variables, kernel.Skills.GetFunction("CodeGenerator", "GenerateCode"));

            string request = result["input"];

            var projectProperty = JsonSerializer.Deserialize<ProjectProperty>(request, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        }

        public static async Task GenerateCode(UserInput userInput, OpenAIConfig openAIConfig, string requirement)
        {
            // Set up kernels
            var builder = new KernelBuilder();

            // Using OpenAI
            builder.WithOpenAIChatCompletionService(openAIConfig.Model, openAIConfig.Key);
            IKernel kernel = builder.Build();

            // Import all skill so all of the function within skill can be used
            var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI", "Plugins");
            kernel.ImportSkill(new FileIOSkill(), "file");
            kernel.ImportSemanticSkillFromDirectory(pluginsDirectory, "CodeGenerator");
            kernel.ImportSkill(new Test.AI.Plugins.CodeGenerator(kernel), nameof(Test.AI.Plugins.CodeGenerator));

            // Modify execute method to implement the solution based on the requirement
            var updateMethodFunction = kernel.Skills.GetFunction("CodeGenerator", "UpdateMethod");
            var extractFilePathAndContentFunction = kernel.Skills.GetFunction("CodeGenerator", "ExtractFilePathAndContent");
            var fileFunction = kernel.Skills.GetFunction("file", "Write");

            var context = new ContextVariables();
            context.Set("path", Path.Combine(userInput.ProjectLocation, "Service.cs"));
            context.Set("input", userInput.Prompt);
            context.Set("requirement", requirement);

            var result = await kernel.RunAsync(context, updateMethodFunction, extractFilePathAndContentFunction, fileFunction);

            // Update csproj file to add some package reference if needed
            var generateCsprojFunction = kernel.Skills.GetFunction("CodeGenerator", "UpdateReference");
            context = new ContextVariables(result["input"]);
            context.Set("path", Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj"));

            result = await kernel.RunAsync(context, generateCsprojFunction, extractFilePathAndContentFunction, fileFunction);
        }
    }
}
