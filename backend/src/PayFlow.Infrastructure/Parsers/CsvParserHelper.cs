using System.Globalization;
using System.Text;

namespace PayFlow.Infrastructure.Parsers;

internal static class CsvParserHelper
{
    public static List<List<string>> ReadCsvRecords(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var records = new List<List<string>>();
        var currentFields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        bool isFirstRecord = true;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!inQuotes && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++; // Skip escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentFields.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (inQuotes)
            {
                sb.Append('\n');
            }
            else
            {
                currentFields.Add(sb.ToString().Trim());
                sb.Clear();

                if (isFirstRecord && currentFields.Count > 0)
                {
                    currentFields[0] = currentFields[0].TrimStart('\uFEFF');
                    isFirstRecord = false;
                }

                // Add record if it has at least one non-empty field
                if (currentFields.Any(f => !string.IsNullOrWhiteSpace(f)))
                {
                    records.Add([.. currentFields]);
                }
                currentFields.Clear();
            }
        }

        // Catch any trailing field if stream ended
        if (currentFields.Count > 0 || sb.Length > 0)
        {
            currentFields.Add(sb.ToString().Trim());
            if (isFirstRecord && currentFields.Count > 0)
            {
                currentFields[0] = currentFields[0].TrimStart('\uFEFF');
            }
            if (currentFields.Any(f => !string.IsNullOrWhiteSpace(f)))
            {
                records.Add([.. currentFields]);
            }
        }

        return records;
    }

    public static string NormalizeKey(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var sb = new StringBuilder(header.Length);
        foreach (char c in header)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    public static Dictionary<string, int> BuildHeaderMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var key = NormalizeKey(headers[i]);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
            {
                map[key] = i;
            }
        }
        return map;
    }

    public static int GetColumnIndex(Dictionary<string, int> headerMap, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizeKey(candidate);
            if (headerMap.TryGetValue(normalized, out int index))
            {
                return index;
            }
        }
        return -1;
    }

    public static string GetFieldValue(List<string> row, int columnIndex, string defaultValue = "")
    {
        if (columnIndex >= 0 && columnIndex < row.Count)
        {
            return row[columnIndex].Trim();
        }
        return defaultValue;
    }

    public static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        var cleaned = value
            .Replace("฿", "")
            .Replace("THB", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Replace(",", "")
            .Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0m;
    }

    public static DateTimeOffset ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateTimeOffset.UtcNow;

        var trimmed = value.Trim();

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt;
        }

        string[] formats = [
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-dd",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd"
        ];

        if (DateTimeOffset.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exactDt))
        {
            return exactDt;
        }

        return DateTimeOffset.UtcNow;
    }
}
