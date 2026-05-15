using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace LuminaVault.Endpoints;

/// One sheet: a name plus a 2D array of cell values. Cells are `object?` so the
/// exporter can pass decimals, dates, bools, etc. — formatting happens in `FormatCell`.
internal record OdsSheet(string Name, object?[][] Rows);

/// Writes `OdsSheet`s to an in-memory .ods file (zip with content.xml + manifest).
/// Inverse of `OdsReader`.
internal static class OdsWriter
{
    public static OdsSheet Sheet(string name, IEnumerable<object?[]> rows) => new(name, rows.ToArray());
    public static object?[] Row(params object?[] values) => values;

    public static byte[] Build(IEnumerable<OdsSheet> sheets)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            // The OpenDocument spec requires `mimetype` to be the first entry, stored uncompressed.
            var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mime.Open()))
                writer.Write("application/vnd.oasis.opendocument.spreadsheet");

            WriteEntry(zip, "content.xml", BuildContentXml(sheets));
            WriteEntry(zip, "styles.xml", """<?xml version="1.0" encoding="UTF-8"?><office:document-styles xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" office:version="1.2"/>""");
            WriteEntry(zip, "meta.xml", """<?xml version="1.0" encoding="UTF-8"?><office:document-meta xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" office:version="1.2"/>""");
            WriteEntry(zip, "META-INF/manifest.xml", """<?xml version="1.0" encoding="UTF-8"?><manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="meta.xml" manifest:media-type="text/xml"/></manifest:manifest>""");
        }
        return stream.ToArray();
    }

    static string BuildContentXml(IEnumerable<OdsSheet> sheets)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.Append("""<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.2"><office:body><office:spreadsheet>""");
        foreach (var sheet in sheets)
        {
            sb.Append($"""<table:table table:name="{Esc(sheet.Name)}">""");
            foreach (var row in sheet.Rows)
            {
                sb.Append("<table:table-row>");
                foreach (var cell in row)
                    sb.Append($"""<table:table-cell office:value-type="string"><text:p>{Esc(FormatCell(cell))}</text:p></table:table-cell>""");
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
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }

    static string FormatCell(object? value) => value switch
    {
        null => "",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "TRUE" : "FALSE",
        _ => value.ToString() ?? ""
    };

    static string Esc(string? value) => WebUtility.HtmlEncode(value ?? "");
}
