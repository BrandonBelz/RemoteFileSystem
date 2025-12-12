namespace RemoteFileSystem.Server;
using System.Net.Sockets;
using RemoteFileSystem.Shared;
using static RemoteFileSystem.Shared.Extras;
using System.Text;

class ClientHandler
{
    private TcpClient _client;
    private ClientState _state = new ClientState();
    public ClientHandler(TcpClient client) { _client = client; }

    public async Task HandleClientAsync()
    {
        Console.WriteLine("Client connected.");
        using (NetworkStream stream = _client.GetStream())
        {
            while (true)
            {
                try
                {
                    Message message =
                        await Message.ReadNextMessageAsync(stream);
                    Console.WriteLine(
                        $"Received message of type {message.Type}, {message.DataLength} bytes.");
                    Message response = ProcessMessage(message);
                    Console.WriteLine(
                        $"Sending message of type {response.Type}, {response.DataLength} bytes.");
                    await response.WriteMessageAsync(stream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }

    private Message ProcessMessage(Message message)
    {
        Message response = new Message();
        string str1;
        string str2;
        string str3;
        byte[] data;
        if (!_state.is_logged_in)
        {
            (str1, str2) = message.Split("\n");
            if (Authenticate(str1, str2))
            {
                try
                {
                    _state.User = str1;
                    response = new Message(MessageType.ResponseLoginSuccess);
                }
                catch (System.Exception)
                {
                    response = Message.NewErrorMessage("User does not exist");
                }
            }
            else
            {
                response = Message.NewErrorMessage("Bad login attempt");
            }
        }
        else
        {
            switch (message.Type)
            {
                case MessageType.RequestRemoveRecursive:
                    str1 = message.GetString();
                    str1 = _state.GetFullPath(str1);
                    if (Directory.Exists(str1) && _state.IsInHome(str1))
                    {
                        if (!_state.CurrentDir.StartsWith(str1))
                        {
                            Directory.Delete(str1, true);
                            response = new(MessageType.ResponseCommandOutput);
                        }
                        else
                        {
                            response = Message.NewErrorMessage(
                                "Cannot remove parent directory " +
                                "of working directory");
                        }
                    }
                    else
                    {
                        response =
                            Message.NewErrorMessage("Directory does not exist");
                    }
                    break;
                case MessageType.RequestRemoveFile:
                    str1 = message.GetString();
                    str1 = _state.GetFullPath(str1);
                    if (File.Exists(str1) && _state.IsInHome(str1))
                    {
                        File.Delete(str1);
                        response = new(MessageType.ResponseCommandOutput);
                    }
                    else
                    {
                        response = Message.NewErrorMessage("File does not exist");
                    }
                    break;
                case MessageType.RequestDownload:
                    str1 = message.GetString();
                    str1 = _state.GetFullPath(str1);
                    if (File.Exists(str1) && _state.IsInHome(str1))
                    {
                        data = File.ReadAllBytes(str1);
                        response = new(MessageType.ResponseCommandOutput, data);
                    }
                    else
                    {
                        response = Message.NewErrorMessage(
                            "Requested file does not exist");
                    }
                    break;
                case MessageType.RequestUpload:
                    (str1, str2) = message.Split("\n");
                    data = Encoding.UTF8.GetBytes(str2);
                    str1 = _state.GetFullPath(str1);
                    str3 = Directory.GetParent(str1)!.FullName +
                           Path.DirectorySeparatorChar;
                    if (Directory.Exists(str3) && _state.IsInHome(str3))
                    {
                        try
                        {
                            File.WriteAllBytes(str1, data);
                            response = new(MessageType.ResponseCommandOutput);
                        }
                        catch (System.Exception)
                        {
                            response = Message.NewErrorMessage(
                                "Invalid destination file name");
                        }
                    }
                    else
                    {
                        response = Message.NewErrorMessage(
                            "Parent directory is invalid or does not exist");
                    }

                    break;
                case MessageType.RequestPrintDirectory:
                    response = new(MessageType.ResponseCommandOutput,
                                   "/" + _state.CurrentDir);
                    break;
                case MessageType.RequestChangeDirectory:
                    try
                    {
                        _state.CurrentDir = message.GetString();
                        response = new(MessageType.ResponseCommandOutput);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        response = Message.NewErrorMessage("Invalid directory");
                    }
                    break;
                case MessageType.RequestMakeDirectory:
                    str1 = message.GetString();
                    str1 = _state.GetFullPath(str1);
                    str2 = Directory.GetParent(str1)!.FullName;
                    if (Directory.Exists(str2) && _state.IsHomeOrInHome(str2))
                    {
                        if (!Directory.Exists(str1))
                        {
                            Directory.CreateDirectory(str1);
                            response = new(MessageType.ResponseCommandOutput);
                        }
                        else
                        {
                            response =
                                Message.NewErrorMessage("Directory already exists");
                        }
                    }
                    else
                    {
                        response = Message.NewErrorMessage(
                            "Parent directory is invalid or does not exist");
                    }
                    break;
                case MessageType.RequestList:
                    str1 = message.GetString();
                    str1 = _state.GetFullPath(str1);
                    if (Directory.Exists(str1) &&
                        str1.StartsWith(_state.homeDirectory))
                    {
                        string[] entries = Directory.GetFileSystemEntries(str1);
                        for (int j = 0; j < entries.Length; j++)
                        {
                            if (Directory.Exists(entries[j]))
                                entries[j] = entries[j][(str1.Length + 1)..] + "/";
                            else
                                entries[j] = entries[j][(str1.Length + 1)..];
                            if (entries[j].Any(char.IsWhiteSpace))
                            {
                                entries[j] = "'" + entries[j] + "'";
                            }
                        }
                        response = new(MessageType.ResponseCommandOutput,
                                       string.Join("\n", entries));
                    }
                    else
                    {
                        response = Message.NewErrorMessage("Directory not found");
                    }

                    break;
                case MessageType.RequestCopy:
                    (str2, str1) = message.Split("\n");
                    str2 = _state.GetFullPath(str2);
                    str1 = _state.GetFullPath(str1);
                    if (File.Exists(str2) && _state.IsInHome(str1) &&
                        _state.IsInHome(str2))
                    {
                        if (!File.Equals(str2, str1))
                        {

                            if (Directory.Exists(str1))
                            {
                                str1 = Path.Combine(str1, Path.GetFileName(str2));
                                File.Copy(str2, str1, true);
                                response = new(MessageType.ResponseCommandOutput);
                            }
                            else if (Directory.Exists(
                                           Directory.GetParent(str1)!.FullName))
                            {
                                File.Copy(str2, str1, true);
                                response = new(MessageType.ResponseCommandOutput);
                            }
                            else
                            {
                                response = Message.NewErrorMessage(
                                    "Destination directory does not exist");
                            }
                        }
                        else
                        {
                            response = Message.NewErrorMessage(
                                "Cannot copy file to itself");
                        }
                    }
                    else
                    {
                        response = Message.NewErrorMessage(
                            "File or directory does not exist");
                    }
                    break;
                case MessageType.RequestCopyRecursive:
                    (str1, str2) = message.Split("\n");
                    str2 = _state.GetFullPath(str2);
                    str1 = _state.GetFullPath(str1);
                    if (Directory.Exists(str2) && _state.IsInHome(str1) &&
                        _state.IsInHome(str2))
                    {
                        if (!str1.StartsWith(str2))
                        {
                            RecursiveCopy(str2, str1);
                            response = new(MessageType.ResponseCommandOutput);
                        }
                        else
                        {
                            response = Message.NewErrorMessage(
                                "Cannot copy directory into itself");
                        }
                    }
                    else
                    {
                        response = Message.NewErrorMessage(
                            "File or directory does not exist");
                    }

                    break;
                default:
                    response = Message.NewErrorMessage("Error in handling request");
                    break;
            }
        }
        return response;
    }

    public static bool Authenticate(string user, string password)
    {
        return true;
    }
}
