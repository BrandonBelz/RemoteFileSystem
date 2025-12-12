namespace RemoteFileSystem.Server;

class Program
{
    static async Task Main()
    {
        Server server = new Server(980);
        await server.StartAsync();
    }
}
