using Test;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

class Program
{
    static async Task Main(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        await new CodeGenerator(configuration).Generate();
    }
}