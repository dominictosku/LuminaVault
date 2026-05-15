using System.IO.Compression;
using System.Xml.Linq;

namespace LuminaVault.Endpoints;

/// Reads an .ods file (which is a zip containing content.xml) into a
/// `Dictionary<sheetName, rows>` shape. Each row is just `List<string>` —
/// cells are coerced to their text representation up-front so callers
/// don't have to know about XML.
internal static class OdsReader
{
    static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static Dictionary<string, List<List<string>>> ReadTables(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entry = zip.GetEntry("content.xml") ?? throw new InvalidDataException("ODS content.xml not found.");
        using var reader = entry.Open();
        var doc = XDocument.Load(reader);
        return doc.Descendants(TableNs + "table")
            .ToDictionary(
                table => table.Attribute(TableNs + "name")?.Value ?? "",
                table => table.Elements(TableNs + "table-row").Select(ReadRow).Where(r => r.Any(c => c.Length > 0)).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    static List<string> ReadRow(XElement row)
    {
        var values = new List<string>();
        foreach (var cell in row.Elements(TableNs + "table-cell"))
        {
            // Clamp `number-columns-repeated` to 100 to prevent malformed sheets from
            // ballooning memory; legitimate spreadsheets never repeat a value 100x.
            var repeat = Math.Min(100, int.TryParse(cell.Attribute(TableNs + "number-columns-repeated")?.Value, out var r) ? r : 1);
            var value = string.Join(" ", cell.Descendants(TextNs + "p").Select(p => p.Value)).Trim();
            if (string.IsNullOrWhiteSpace(value))
                value = cell.Attribute(OfficeNs + "value")?.Value ?? cell.Attribute(OfficeNs + "date-value")?.Value ?? "";
            for (var i = 0; i < repeat; i++) values.Add(value);
        }
        return values;
    }
}
