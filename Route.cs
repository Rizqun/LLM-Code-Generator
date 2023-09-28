using CodeGenerator.Constants;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace CodeGenerator
{
    public class Route
    {
        private IConfiguration _configuration;

        public Route(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static string MainPage()
        {
            return AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                        .Title($"[{Theme.Primary}]What do you want to do?[/]")
                        .PageSize(9)
                        .HighlightStyle($"{Theme.Secondary}")
                        .AddChoiceGroup("Embedding", new[]
                        {
                            EmbeddingPurpose.ListCollection,
                            EmbeddingPurpose.AddNewCollection,
                            EmbeddingPurpose.DeleteCollection
                        })
                        .AddChoiceGroup("Generate", new[]
                        {
                            GeneratePurpose.GenerateFromJira,
                            GeneratePurpose.GenerateFromAPI,
                            GeneratePurpose.GenerateFromJiraAndAPI
                        }));
        }

        public async Task Back()
        {
            Console.Write("\nGo back [y]? ");
            var confirmation = Console.ReadLine();

            if (confirmation!.ToLower() == "y")
            {
                Console.Clear();
                await new Generator(_configuration).Generate();
            }
        }
    }
}
