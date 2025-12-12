namespace RemoteFileSystem.Server;
using System.Net.Sockets;
using System.Net;

class Server {
    private TcpListener _listener;

    public Server(int port) {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task StartAsync() {
        _listener.Start();
        Console.WriteLine("Server started.");

        while (true) {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            ClientHandler handler = new ClientHandler(client);
            _ = Task.Run(() => handler.HandleClientAsync());
        }
    }
}
