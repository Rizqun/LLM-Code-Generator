using Project;

class Program
{
    static async Task Main(string[] args)
    {
        await new Service().Execute();
    }
}