# RemoteFileSystem

A small, easy-to-run .NET 8 TCP-based remote file system with a command-line client and server.  
Designed for local testing and learning — it provides basic file operations (ls, cd, upload, download, copy, remove, mkdir) scoped to a per-user home directory.

This README explains how the project is organized, how to set up the DATA directory used by the server, how to build and run the server and client, and how to use the client commands.

---

## TL;DR

- Server listens on port 980 (TCP).
- Client connects to server and authenticates with username/password (authentication is permissive by default).
- Server stores user data in a DATA directory that is a sibling of the repository directory (see details below).
- File upload limit from the client: 50 MB.
- Target framework: .NET 8.0

---

## Project layout

- RemoteFileSystem.sln
- Client/
  - Client.cs — connection, login flow, main prompt loop
  - CommandHandler.cs — CLI command parsing & handlers
  - Program.cs — client entry point
- Server/
  - Server.cs — listener & client accept loop
  - ClientHandler.cs — request processing & filesystem operations
  - ClientState.cs — per-connection state and path scoping
  - Program.cs — server entry point
- Shared/
  - Message.cs — framing and utilities for the custom TCP protocol
  - MessageType.cs — request/response enum
  - Extras.cs — small helpers (RecursiveCopy, NormalizePath)

---

## Important: DATA directory location

The server constructs the DATA directory path as:

ClientState.GlobalDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "../../../../../DATA"));

When running from build output (for example `bin/Debug/net8.0`), that resolves to a DATA directory that is a sibling of the repository root. In other words:

- parent-dir/
  - RemoteFileSystem/        ← this repository
  - DATA/                    ← expected place for user data (sibling of the repo)
    - alice/
    - bob/

Create a directory for each test user inside DATA before attempting to log in as that user.

Example (from inside the repository):
- Move one level up to the parent directory and create the DATA and user directories:

  cd ..
  mkdir DATA
  mkdir DATA/alice
**Add files to DATA/alice to test ls/down/cp**

---

## Build

Requirements:
- .NET 8 SDK

From the repository root:

1. Restore and build:

   dotnet restore
   dotnet build RemoteFileSystem.sln

2. (Optional) If you prefer to run from Visual Studio / Rider, open RemoteFileSystem.sln.

---

## Run

1. Ensure the DATA directory (sibling of repo) and at least one user directory exist (see above).

2. Start the server (from repo root):

   dotnet run --project Server

   You should see:
   Server started.

3. Start the client (in another terminal):

   dotnet run --project Client

   Client will prompt for the server IP (defaults to 127.0.0.1 if blank), then ask for Username and Password.

Notes:
- The server's Authenticate method currently returns true (no password validation). The server will accept any credentials but will only attach you to a user directory that already exists under DATA.
- The client will not upload files larger than 50 MB.

---

## Client commands

After successful login you will see a prompt (>>). Basic usage:

- up <local_source_path> <remote_destination_path>
  - Upload a file to the server.
  - Example:
    up "./my notes.txt" /docs/"my notes.txt"
  - Max file size: 50 MB.

- down <remote_source_path> <local_destination_path>
  - Download a remote file to a local path.
  - Example:
    down /docs/report.pdf ./report.pdf

- cd [path]
  - Change remote working directory. Defaults to `/` (home).
  - Example:
    cd /projects

- pwd
  - Print remote current working directory.

- mkdir <dir>
  - Create a directory relative to current directory or as an absolute path (starting with `/`).

- ls [directory]
  - List files and directories (directories are suffixed with `/`). Default is the current directory.
  - Example:
    ls
    ls /docs

- rm <path> [--recursive|-r]
  - Remove a file or a directory. Use `--recursive` for directories.

- cp <source> <destination> [--recursive|-r]
  - Copy a file or directory (use `--recursive` to copy directories).

- exit
  - Exit the client.

Notes:
- Paths starting with `/` are treated as absolute inside the user's home directory. All operations are constrained to the user's home directory.
- Paths that contain whitespace may be quoted with double quotes.

---

## Example session

Below is an example showing the server terminal and a client session side-by-side (server running in one terminal, client in another).

Server terminal:
```bash
$ dotnet run --project Server
Server started.
# (server will accept connections and log activity)
```

Client terminal:
```bash
$ dotnet run --project Client
Input IP address of server (leave blank if running on this machine): [press Enter]
Connecting to server...
Connection success!
Username: alice
Password: secret
Successfully logged in as alice.
>> pwd
/                       # root of alice's home directory
>> ls
docs/
notes.txt
>> up "./local.txt" /docs/remote.txt
# (no output on success; server stores uploaded file at DATA/alice/docs/remote.txt)
>> ls /docs
remote.txt
>> down /docs/remote.txt "./downloaded.txt"
# (downloads remote file to local ./downloaded.txt)
>> exit
# client exits
```

Quick notes about the example:
- The `Input IP address...` prompt accepts an empty response to connect to localhost (127.0.0.1).
- Login is permissive by default; the server will attach you to the `alice` directory only if `DATA/alice` exists.
- `ls` prints directories with a trailing `/`. Entries with whitespace are quoted by the client when necessary.

---

## Protocol (brief)

Messages are framed with:
- 1 byte: MessageType (see Shared/MessageType.cs)
- 4 bytes: payload length (big-endian / network order)
- N bytes: payload

Common requests:
- RequestLogin: payload "username\npassword"
- RequestUpload: payload "path\n{file_bytes}" (client places file bytes after newline)
- RequestDownload: payload "path"
- ResponseCommandOutput: text responses or file bytes
- ResponseError: UTF-8 error string
See `Shared/Message.cs` and `Shared/MessageType.cs` for implementation details.

---

## Limitations & Security

This project is intentionally tiny for demonstration and local testing. Do NOT run this server exposed to untrusted networks without hardening:

- Authentication is effectively disabled (Authenticate returns true). Implement proper authentication and credential storage before public use.
- No encryption (plain TCP). Add TLS if you need confidentiality/integrity.
- The client enforces a 50 MB upload limit but server-side checks and hardening should be added.
- Minimal input validation; path handling is conservative but review before making changes.
- No logging/auditing, no rate limiting, no sandboxing of executed operations.

---

## Future Work

- Implement proper authentication (password store, hashing, salted passwords).
- Add TLS support for the server and client.
- Add configuration options (port, DATA directory location).
- Add unit and integration tests for client/server message handling.
- Improve file transfer protocol to support streaming large files (instead of reading full file into memory).
- Implement a simple file-based authentication mechanism for demo purposes.

Which would you like me to do next?
