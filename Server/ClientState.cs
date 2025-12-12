namespace RemoteFileSystem.Server;
using System.IO;

class ClientState
{
    private string _user;
    public string User
    {
        get { return _user; }
        set
        {
            if (_user == "" &&
                Directory.Exists(Path.Combine(GlobalDir, value)))
            {
                _user = value;
            }
            else
            {
                throw new Exception("Client already assigned a user.");
            }
        }
    }
    public string homeDirectory
    {
        get { return Path.Combine(GlobalDir, User); }
    }
    private string _current_dir = "";
    public string CurrentDir
    {
        get { return _current_dir; }
        set
        {
            string path;
            if (value == "/")
            {
                path = homeDirectory;
            }
            else if (value[0] == '/')
            {
                path = Path.Combine(homeDirectory,
                                    value[1..]); // Remove leading /
            }
            else
            {
                path = Path.Combine(homeDirectory, _current_dir, value);
            }

            string fullPath = Path.GetFullPath(path);
            string homeFullPath = Path.GetFullPath(homeDirectory);

            string homeWithSeparator =
                homeFullPath + Path.DirectorySeparatorChar;

            if (Directory.Exists(fullPath) &&
                (fullPath.StartsWith(homeWithSeparator) ||
                 fullPath == homeFullPath))
            {
                string newDir;
                if (fullPath == homeFullPath)
                {
                    newDir = "";
                }
                else
                {
                    newDir = fullPath[(homeWithSeparator.Length)..];
                }
                _current_dir = newDir;
            }
            else
            {
                throw new Exception(
                    $"Invalid directory for user. Path: '{fullPath}', Home: '{homeFullPath}', Exists: {Directory.Exists(fullPath)}");
            }
        }
    }
    public bool is_logged_in
    {
        get { return User != ""; }
    }

    public static string GlobalDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../DATA"));

    public ClientState() { _user = ""; }

    public string GetFullPath(string path)
    {
        if (path[0] == '/')
        {
            path = Path.Combine(homeDirectory,
                                path[1..]); // Remove leading /
        }
        else
        {
            path = Path.Combine(homeDirectory, CurrentDir, path);
        }
        if (path[^1] == '/')
        {
            path = path[..(path.Length - 1)];
        }
        return path;
    }

    public bool IsInHome(string path)
    {
        return path.StartsWith(homeDirectory + Path.DirectorySeparatorChar);
    }
    public bool IsHomeOrInHome(string path)
    {
        return path.StartsWith(homeDirectory + Path.DirectorySeparatorChar) ||
               path.Equals(homeDirectory);
    }
}
