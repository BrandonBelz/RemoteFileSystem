namespace RemoteFileSystem.Client;
using System.CommandLine;
using System.Net.Sockets;
using System.Text;
using RemoteFileSystem.Shared;
using static RemoteFileSystem.Shared.Extras;

public class CommandHandler {
    private RootCommand _rootCommand;
    private NetworkStream _stream;

    public CommandHandler(NetworkStream stream) {
        _stream = stream;
        _rootCommand = new RootCommand("Remote File System");

        // Upload Command
        Command uploadFile = new("up", "Upload a file to the server");
        Argument<string> sourcePathArg =
            new("source_path") { Arity = ArgumentArity.ExactlyOne };
        Argument<string> destinationPathArg =
            new("destination_path") { Arity = ArgumentArity.ExactlyOne };
        uploadFile.Add(sourcePathArg);
        uploadFile.Add(destinationPathArg);
        uploadFile.SetAction(parseResult => HandleUpload(
                                 parseResult.GetValue(sourcePathArg)!,
                                 parseResult.GetValue(destinationPathArg)!));
        _rootCommand.Subcommands.Add(uploadFile);

        // Download Command
        Command downloadFile = new("down", "Download a file from the server");
        downloadFile.Add(sourcePathArg);
        downloadFile.Add(destinationPathArg);
        downloadFile.SetAction(parseResult => HandleDownload(
                                   parseResult.GetValue(sourcePathArg)!,
                                   parseResult.GetValue(destinationPathArg)!));
        _rootCommand.Subcommands.Add(downloadFile);

        // Change Directory Command
        Command changeDirectory = new("cd", "Change working directory");
        Argument<string> pathArg =
            new("path") { DefaultValueFactory = parseResult => "/" };
        changeDirectory.Add(pathArg);
        changeDirectory.SetAction(parseResult => SinglePathNoOutput(
                                      MessageType.RequestChangeDirectory,
                                      parseResult.GetValue(pathArg)!));
        _rootCommand.Subcommands.Add(changeDirectory);

        // Print Working Directory Command
        Command printDirectory = new("pwd", "Print working directory");
        printDirectory.SetAction(parseResult => HandlePWD());
        _rootCommand.Subcommands.Add(printDirectory);

        // Make Directory command
        Command makeDirectory = new("mkdir", "Make new directory");
        Argument<string> dirArg =
            new("dir") { Arity = ArgumentArity.ExactlyOne };
        makeDirectory.Add(dirArg);
        makeDirectory.SetAction(
            parseResult => SinglePathNoOutput(MessageType.RequestMakeDirectory,
                                              parseResult.GetValue(dirArg)!));
        _rootCommand.Subcommands.Add(makeDirectory);

        // List command
        Command listCmd = new("ls", "List files and subdirectories");
        Argument<string> directoryArg =
            new("directory") { DefaultValueFactory = parseResult => "." };
        listCmd.Add(directoryArg);
        listCmd.SetAction(parseResult =>
                              HandleLs(parseResult.GetValue(directoryArg)!));
        _rootCommand.Subcommands.Add(listCmd);

        // Remove command
        Command removeFile = new("rm", "Remove a file");
        removeFile.Add(dirArg);
        Option<bool> recursive = new("--recursive", "-r");
        recursive.Description =
            "Perform command on a directory and its contents recursively";
        removeFile.Options.Add(recursive);
        removeFile.SetAction(
            parseResult => SinglePathNoOutput(MessageType.RequestRemoveFile,
                                              parseResult.GetValue(dirArg)!,
                                              parseResult.GetValue(recursive)));
        _rootCommand.Subcommands.Add(removeFile);

        // Copy command
        Command copyCommand = new("cp", "Copy file");
        copyCommand.Add(sourcePathArg);
        copyCommand.Add(destinationPathArg);
        copyCommand.Options.Add(recursive);
        copyCommand.SetAction(
            parseResult => HandleCopy(parseResult.GetValue(sourcePathArg)!,
                                      parseResult.GetValue(destinationPathArg)!,
                                      parseResult.GetValue(recursive)));
        _rootCommand.Subcommands.Add(copyCommand);
    }

    public async Task HandleCommand(string command) {
        var args = new List<string>();
        var currentArg = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < command.Length; i++) {
            char c = command[i];

            if (c == '"') {
                inQuotes = !inQuotes;
            } else if (c == ' ' && !inQuotes) {
                if (currentArg.Length > 0) {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            } else {
                currentArg.Append(c);
            }
        }

        if (currentArg.Length > 0) {
            args.Add(currentArg.ToString());
        }

        await _rootCommand.Parse(args.ToArray()).InvokeAsync();
    }

    private async Task HandleUpload(string sourcePath, string destinationPath) {
        if (File.Exists(sourcePath)) {
            try {
                destinationPath = NormalizePath(destinationPath);
                FileInfo srcInfo = new(sourcePath);
                if (srcInfo.Length <= 52428800) {
                    byte[] srcBytes = File.ReadAllBytes(sourcePath);
                    byte[] msgBytesPartial =
                        Encoding.UTF8.GetBytes(destinationPath + "\n");
                    var msgBytesFull =
                        new byte[srcBytes.Length + msgBytesPartial.Length];
                    Array.Copy(msgBytesPartial, 0, msgBytesFull, 0,
                               msgBytesPartial.Length);
                    Array.Copy(srcBytes, 0, msgBytesFull,
                               msgBytesPartial.Length, srcBytes.Length);
                    // Console.WriteLine(Encoding.UTF8.GetString(msgBytesFull));

                    Message message =
                        new(MessageType.RequestUpload, msgBytesFull);
                    await message.WriteMessageAsync(_stream);
                    Message response =
                        await Message.ReadNextMessageAsync(_stream);
                    if (response.Type == MessageType.ResponseError) {
                        Console.WriteLine($"Error: {response.GetString()}");
                    }
                } else {
                    Console.WriteLine(
                        "Error: Files larger than 50 MB are not supported.");
                }
            } catch (System.Exception) {
                Console.WriteLine("Error: Permission denied for local file");
            }
        } else {
            Console.WriteLine("Error: File not found.");
        }
    }

    private async Task HandleDownload(string sourcePath,
                                      string destinationPath) {
        sourcePath = NormalizePath(sourcePath);
        Message request = new(MessageType.RequestDownload, sourcePath);
        await request.WriteMessageAsync(_stream);
        Message response = await Message.ReadNextMessageAsync(_stream);
        if (response.Type == MessageType.ResponseCommandOutput) {
            if (Directory.Exists(Path.GetDirectoryName(destinationPath))) {
                try {
                    File.WriteAllBytes(destinationPath, response.DataArray);
                } catch (System.Exception) {
                    Console.WriteLine(
                        "Error: Permission denied for destination directory");
                }
            } else {
                Console.WriteLine("Error: Destination directory not found");
            }
        } else if (response.Type == MessageType.ResponseError) {
            Console.WriteLine($"Error: {response.GetString()}");
        }
    }
    private async Task HandleCopy(string sourcePath, string destPath,
                                  bool recursive = false) {
        sourcePath = NormalizePath(sourcePath);
        destPath = NormalizePath(destPath);

        MessageType type;
        if (recursive) {
            type = MessageType.RequestCopyRecursive;
        } else {
            type = MessageType.RequestCopy;
        }

        Message message = new(type, sourcePath + "\n" + destPath);
        await message.WriteMessageAsync(_stream);
        Message response = await Message.ReadNextMessageAsync(_stream);
        if (response.Type == MessageType.ResponseError) {
            Console.WriteLine($"Error: {response.GetString()}");
        }
    }

    // Used for cd, mkdir, rm
    private async Task SinglePathNoOutput(MessageType type, string path,
                                          bool recursive = false) {
        if (type == MessageType.RequestRemoveFile && recursive) {
            type = MessageType.RequestRemoveRecursive;
        }
        path = NormalizePath(path);
        Message message = new(type, Encoding.UTF8.GetBytes(path));
        await message.WriteMessageAsync(_stream);
        Message response = await Message.ReadNextMessageAsync(_stream);
        if (response.Type == MessageType.ResponseError) {
            Console.WriteLine($"Error: {response.GetString()}");
        }
    }

    private async Task HandlePWD() {
        Message message = new(MessageType.RequestPrintDirectory);
        await message.WriteMessageAsync(_stream);
        Message response = await Message.ReadNextMessageAsync(_stream);
        if (response.Type == MessageType.ResponseError) {
            Console.WriteLine($"Error: {response.GetString()}");
        } else if (response.Type == MessageType.ResponseCommandOutput) {
            Console.WriteLine(response.GetString());
        }
    }

    private async Task HandleLs(string dir) {
        dir = NormalizePath(dir);
        Message message = new(MessageType.RequestList, dir);
        await message.WriteMessageAsync(_stream);
        Message response = await Message.ReadNextMessageAsync(_stream);
        if (response.Type == MessageType.ResponseError) {
            Console.WriteLine($"Error: {response.GetString()}");
        } else if (response.Type == MessageType.ResponseCommandOutput &&
                   response.DataLength > 0) {
            Console.WriteLine(response.GetString());
        }
    }
}
