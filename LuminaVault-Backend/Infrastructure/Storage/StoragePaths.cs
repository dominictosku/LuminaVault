namespace LuminaVault.Storage;

/// Resolves on-disk paths for the SQLite DB, uploads, and logs.
/// Defaults to ContentRoot/data for local dev; container deployments set LUMINA_DATA_DIR
/// to a mounted volume so all stateful files live in one place that survives image rebuilds.
public sealed class StoragePaths
{
    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "luminavault.db");
    public string UploadsDirectory => Path.Combine(DataDirectory, "uploads");
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public StoragePaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
    }

    public string UploadPath(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            throw new ArgumentException("Upload file name is required.", nameof(fileName));
        return Path.Combine(UploadsDirectory, safeFileName);
    }
}
