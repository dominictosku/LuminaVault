namespace LuminaVault.Storage;

internal static class StorageBootstrap
{
    public static StoragePaths Resolve(string contentRootPath, string? dataDirOverride)
    {
        var storage = new StoragePaths(string.IsNullOrWhiteSpace(dataDirOverride)
            ? Path.Combine(contentRootPath, "data")
            : dataDirOverride);

        if (string.IsNullOrWhiteSpace(dataDirOverride))
            MoveLegacyRootState(contentRootPath, storage);

        Directory.CreateDirectory(storage.DataDirectory);
        Directory.CreateDirectory(storage.UploadsDirectory);
        Directory.CreateDirectory(storage.LogsDirectory);
        return storage;
    }

    private static void MoveLegacyRootState(string contentRootPath, StoragePaths storage)
    {
        Directory.CreateDirectory(storage.DataDirectory);

        var legacyDbPath = Path.Combine(contentRootPath, "luminavault.db");
        if (MoveFileIfTargetMissing(legacyDbPath, storage.DatabasePath))
        {
            MoveFileIfTargetMissing($"{legacyDbPath}-wal", $"{storage.DatabasePath}-wal");
            MoveFileIfTargetMissing($"{legacyDbPath}-shm", $"{storage.DatabasePath}-shm");
        }

        MoveDirectoryContents(Path.Combine(contentRootPath, "uploads"), storage.UploadsDirectory);
        MoveDirectoryContents(Path.Combine(contentRootPath, "logs"), storage.LogsDirectory);
        MoveDirectoryContents(Path.Combine(contentRootPath, "backups"), Path.Combine(storage.DataDirectory, "backups"));
    }

    private static bool MoveFileIfTargetMissing(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination);
        return true;
    }

    private static void MoveDirectoryContents(string source, string destination)
    {
        if (!Directory.Exists(source)) return;

        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path.Length))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            if (File.Exists(target)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target);
        }

        TryDeleteEmptyDirectory(source);
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch
        {
            // Leaving a legacy folder behind is harmless if a file was locked or collided.
        }
    }
}
