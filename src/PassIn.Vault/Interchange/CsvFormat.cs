using System.Text;

namespace PassIn.Vault.Interchange;

/// <summary>Minimal RFC 4180 reader and writer.</summary>
internal static class CsvFormat
{
    public static IEnumerable<string[]> Read(TextReader reader)
    {
        var record = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int c;

        while ((c = reader.Read()) != -1)
        {
            char ch = (char)c;
            if (inQuotes)
            {
                if (ch != '"')
                {
                    field.Append(ch);
                }
                else if (reader.Peek() == '"')
                {
                    field.Append('"');
                    reader.Read();
                }
                else
                {
                    inQuotes = false;
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    yield return record.ToArray();
                    record.Clear();
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            yield return record.ToArray();
        }
    }

    public static void WriteRecord(TextWriter writer, IEnumerable<string> fields)
    {
        writer.Write(string.Join(',', fields.Select(Escape)));
        writer.Write("\r\n");
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
