using System.Text;

namespace LuminaVault.Endpoints;

internal record BankCsvTable(string[] Headers, List<string[]> Rows);

internal static class BankCsvReader
{
    public static BankCsvTable Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        var delimiter = DetectDelimiter(text);
        var rows = Parse(text, delimiter)
            .Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
            .ToList();

        if (rows.Count == 0) return new BankCsvTable([], []);

        var headers = rows[0].Select(h => h.Trim().Trim('\uFEFF')).ToArray();
        return new BankCsvTable(headers, rows.Skip(1).ToList());
    }

    private static char DetectDelimiter(string text)
    {
        var candidates = new[] { ';', ',', '\t' };
        var best = ',';
        var bestScore = -1;
        foreach (var candidate in candidates)
        {
            var score = text
                .Split('\n')
                .Take(8)
                .Sum(line => CountOutsideQuotes(line, candidate));
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                quoted = !quoted;
            }
            else if (!quoted && line[i] == delimiter)
            {
                count++;
            }
        }
        return count;
    }

    private static List<string[]> Parse(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (!quoted && c == delimiter)
            {
                row.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else if (!quoted && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString().Trim());
                cell.Clear();
                rows.Add(row.ToArray());
                row = [];
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString().Trim());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
