using System.IO.Compression;

namespace LuminaVault.Tests;

public class BackupTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public BackupTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Backup_endpoint_returns_zip_with_database()
    {
        await _api.EnsureAuthedAsync();
        var resp = await _api.Raw.GetAsync("/api/data/backup");
        resp.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", resp.Content.Headers.ContentType?.MediaType);

        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, entry => entry.FullName == "luminavault.db");
    }
}
