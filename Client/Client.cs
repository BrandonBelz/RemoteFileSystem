namespace RemoteFileSystem.Client;
using RemoteFileSystem.Shared;
using System.Net.Sockets;
using System.Text;

class Client
{
    private TcpClient _tcpClient = new TcpClient();
    public string ServerIP { get; private set; }
    public static int ServerPort { get; } = 980;

    public Client(string ip) { ServerIP = ip; }

    public async Task StartAsync()
    {
        Console.WriteLine("Connecting to server...");
        await _tcpClient.ConnectAsync(ServerIP, ServerPort);
        Console.WriteLine("Connection success!");

        Console.Write("Username: ");
        string? user = Console.ReadLine();
        Console.Write("Password: ");
        string? passw = Console.ReadLine();

        using (NetworkStream stream = _tcpClient.GetStream())
        {
            Message loginMsg =
                new Message(MessageType.RequestLogin,
                            Encoding.UTF8.GetBytes(user + "\n" + passw));
            await loginMsg.WriteMessageAsync(stream);
            Message rsp = await Message.ReadNextMessageAsync(stream);
            while (rsp.Type != MessageType.ResponseLoginSuccess)
            {
                string rspMsg = Encoding.UTF8.GetString(rsp.Data.ToArray());
                if (rspMsg != "")
                {
                    Console.WriteLine(rspMsg);
                }
                Console.Write("Username: ");
                user = Console.ReadLine();
                Console.Write("Password: ");
                passw = Console.ReadLine();
                loginMsg =
                    new Message(MessageType.RequestLogin,
                                Encoding.UTF8.GetBytes(user + "\n" + passw));
                await loginMsg.WriteMessageAsync(stream);
                rsp = await Message.ReadNextMessageAsync(stream);
            }
            Console.WriteLine($"Successfully logged in as {user}.");
            await MainLoop(stream);
        }
    }

    private static async Task MainLoop(NetworkStream stream)
    {
        CommandHandler commandHandler = new(stream);
        string userCommand = "";
        do
        {
            if (userCommand != "")
            {
                try
                {
                    await commandHandler.HandleCommand(userCommand);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            Console.Write(">> ");
            userCommand = Console.ReadLine()!.Trim();
        } while (userCommand != "exit");
    }
}
