namespace RemoteFileSystem.Client;

class Program
{
    static async Task Main()
    {
        Console.Write("Input IP address of server (leave blank if " +
                      "running on this machine): ");
        string? ip = Console.ReadLine();
        if (ip == null || ip == "")
        {
            ip = "127.0.0.1";
        }
        Client client = new Client(ip);
        await client.StartAsync();
    }
}
