namespace RemoteFileSystem.Shared;

public static class Extras {
    public static void RecursiveCopy(string source, string dest) {
        var dir = new DirectoryInfo(source);

        if (!dir.Exists)
            throw new DirectoryNotFoundException(
                $"Source directory not found: {source}");

        Directory.CreateDirectory(dest);

        // Copy files
        foreach (FileInfo file in dir.GetFiles()) {
            string targetPath = Path.Combine(dest, file.Name);
            file.CopyTo(targetPath, overwrite: true);
        }

        // Copy subdirectories
        foreach (DirectoryInfo subDir in dir.GetDirectories()) {
            string newDestDir = Path.Combine(dest, subDir.Name);
            RecursiveCopy(subDir.FullName, newDestDir);
        }
    }

    public static string NormalizePath(string path) {
        return string.Join("/", path.Split(Path.DirectorySeparatorChar));
    }
}
