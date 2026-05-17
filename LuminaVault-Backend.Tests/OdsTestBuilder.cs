using System.IO.Compression;
using System.Net;
using System.Text;

namespace LuminaVault.Tests;

/// Minimal in-memory ODS builder for tests that need to inject hand-crafted
/// spreadsheet rows (e.g. malformed splits) the API surface won't let us POST.
internal static class OdsTestBuilder
{
    public static byte[] Build(params (string Sheet, string[][] Rows)[] sheets)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = new StreamWriter(mime.Open()))
                w.Write("application/vnd.oasis.opendocument.spreadsheet");

            WriteEntry(zip, "content.xml", BuildContent(sheets));
            WriteEntry(zip, "META-INF/manifest.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.2\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.spreadsheet\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>");
        }
        return stream.ToArray();
    }

    static string BuildContent((string Sheet, string[][] Rows)[] sheets)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:spreadsheet>");
        foreach (var (name, rows) in sheets)
        {
            sb.Append($"<table:table table:name=\"{Esc(name)}\">");
            foreach (var row in rows)
            {
                sb.Append("<table:table-row>");
                foreach (var cell in row)
                    sb.Append($"<table:table-cell office:value-type=\"string\"><text:p>{Esc(cell)}</text:p></table:table-cell>");
                sb.Append("</table:table-row>");
            }
            sb.Append("</table:table>");
        }
        sb.Append("</office:spreadsheet></office:body></office:document-content>");
        return sb.ToString();
    }

    static void WriteEntry(ZipArchive zip, string path, string contents)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var w = new StreamWriter(entry.Open());
        w.Write(contents);
    }

    static string Esc(string? value) => WebUtility.HtmlEncode(value ?? "");
}
