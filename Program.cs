using CodeGenerator;
using Microsoft.Extensions.Configuration;
class Program
{
    static async Task Main(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        await new Generator(configuration).Generate();
    }
}