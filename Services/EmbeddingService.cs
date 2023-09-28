using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel;
using CodeGenerator.Constants;
using Spectre.Console;
using CodeGenerator.Models;
using Microsoft.Extensions.Configuration;
using CodeGenerator.Models.Response;

namespace CodeGenerator.Services
{
    public class EmbeddingService
    {
        private IKernel _kernel;
        private IConfiguration _configuration;

        public EmbeddingService(IKernel kernel, IConfiguration configuration)
        {
            _kernel = kernel;
            _configuration = configuration;
        }

        public async Task ListCollection(QdrantConfig qdrantConfig)
        {
            var collections = await _kernel.Memory.GetCollectionsAsync();
            var collectionsInfo = new List<CollectionInfo>();

            var table = new Table();
            table.Border(TableBorder.Horizontal);
            table.AddColumn($"[{Theme.Primary}]No[/]");
            table.AddColumn($"[{Theme.Primary}]Collection Name[/]");
            table.AddColumn(new TableColumn($"[{Theme.Primary}]Vectors[/]").RightAligned());

            var count = 1;
            foreach ( var collection in collections)
            {
                var httpClientService = new HttpClientService($"{qdrantConfig.Host}/collections/{collection}");
                var collectionInfo = await httpClientService.GetWithApiKeyAsync<CollectionInfo>(qdrantConfig.Key);

                collectionsInfo.Add(collectionInfo);
                table.AddRow($"[{Theme.Secondary}]{count}[/]", $"[{Theme.Secondary}]{collection}[/]", $"[{Theme.Secondary}]{collectionInfo.Result.VectorsCount}[/]");
                count++;
            }

            AnsiConsole.Write(table);

            await new Route(_configuration).Back();
        }

        public async Task AddNewCollection()
        {
            AnsiConsole.Write(new Markup($"[{Theme.Primary}]Collection Name:[/]\n" + $"[{Theme.Secondary}]> [/]"));
            var collectionName = Console.ReadLine();

            AnsiConsole.Write(new Markup($"\n[{Theme.Primary}]API documentation URL (separated by comma):[/]\n" + $"[{Theme.Secondary}]> [/]"));
            var documentationURL = Console.ReadLine();

            if (!string.IsNullOrEmpty(collectionName) && !string.IsNullOrEmpty(documentationURL))
            {
                var documentationContent = await WebScrapingService.GetWebContent(documentationURL);
                await InsertDocumentationToMemory(collectionName, documentationContent);
            }

            await new Route(_configuration).Back();
        }

        public async Task DeleteCollection(QdrantConfig qdrantConfig)
        {
            AnsiConsole.Write(new Markup($"[{Theme.Primary}]Collection Name:[/]\n" + $"[{Theme.Secondary}]> [/]"));
            var collectionName = Console.ReadLine();

            if (!string.IsNullOrEmpty(collectionName))
            {
                var httpClientService = new HttpClientService($"{qdrantConfig.Host}/collections/{collectionName}");
                await httpClientService.DeleteWithApiKeyAsync(qdrantConfig.Key);
            }

            await new Route(_configuration).Back();
        }

        private async Task InsertDocumentationToMemory(string collectionName, string documentationContent)
        {
            var lines = TextChunker.SplitPlainTextLines(documentationContent, 100);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 500);

            foreach (var paragraph in paragraphs)
            {
                try
                {
                    await _kernel.Memory.SaveInformationAsync(
                        collection: collectionName,
                        text: paragraph,
                        id: Guid.NewGuid().ToString()
                    );
                }
                catch (Exception) { }
            }
        }
    }
}
