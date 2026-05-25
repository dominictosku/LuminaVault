namespace LuminaVault.Storage;

public sealed record StoredUpload(
    string FileName,
    string OriginalFileName,
    string ContentType,
    long Size,
    string Extension);

public sealed record SaveUploadResult(StoredUpload? Upload, string? Error);

public sealed class UploadSaveOptions
{
    public required long MaxBytes { get; init; }
    public required string MaxSizeMessage { get; init; }
    public string[] AllowedExtensions { get; init; } = Array.Empty<string>();
    public string[] AllowedContentTypes { get; init; } = Array.Empty<string>();
    public string UnsupportedTypeMessage { get; init; } = "Unsupported file type.";
    public string DefaultContentType { get; init; } = "application/octet-stream";
    public Func<string, string?, string>? ContentTypeForExtension { get; init; }
}

public sealed class UploadStorage
{
    private readonly StoragePaths _storage;

    public UploadStorage(StoragePaths storage)
    {
        _storage = storage;
    }

    public async Task<SaveUploadResult> SaveAsync(
        IFormFile? file,
        UploadSaveOptions options,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return new SaveUploadResult(null, "No file.");
        if (file.Length > options.MaxBytes)
            return new SaveUploadResult(null, options.MaxSizeMessage);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (options.AllowedExtensions.Length > 0 &&
            (string.IsNullOrWhiteSpace(extension) ||
             !options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)))
        {
            return new SaveUploadResult(null, options.UnsupportedTypeMessage);
        }

        if (options.AllowedContentTypes.Length > 0 &&
            !options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return new SaveUploadResult(null, options.UnsupportedTypeMessage);
        }

        var contentType = options.ContentTypeForExtension?.Invoke(extension, file.ContentType)
            ?? (string.IsNullOrWhiteSpace(file.ContentType) ? options.DefaultContentType : file.ContentType);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        Directory.CreateDirectory(_storage.UploadsDirectory);
        await using (var fs = File.Create(_storage.UploadPath(storedFileName)))
            await file.CopyToAsync(fs, ct);

        return new SaveUploadResult(new StoredUpload(
            storedFileName,
            Path.GetFileName(file.FileName),
            contentType,
            file.Length,
            extension), null);
    }

    public async Task<byte[]?> ReadAsync(string? fileName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var path = _storage.UploadPath(fileName);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public void DeleteIfExists(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            var path = _storage.UploadPath(fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; deleting DB rows must not fail because the file
            // was already gone or temporarily locked.
        }
    }
}
