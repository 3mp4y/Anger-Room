using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AngerRoom.Shimmer
{
    // All saved CSV files: explicit Excel delimiter, UTF-8 BOM and decimal comma.
    // The native bridge payload stays invariant; legacy saved CSVs remain readable.
    public static class RecordingCsv
    {
        public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture).Replace('.', ',');
        public static string Field(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        public static double ParseNumber(string value) => double.Parse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        public static string LocalizeNumber(string invariantValue) => invariantValue.Replace('.', ',');
        public static StreamWriter Create(string path, string commaSeparatedHeader, bool append = false)
        {
            bool hasContent = append && File.Exists(path) && new FileInfo(path).Length > 0;
            if (hasContent)
                using (var reader = new StreamReader(path))
                    if (reader.ReadLine() != "sep=;") throw new InvalidDataException("Cannot append Excel rows to a legacy CSV: " + path);
            var writer = new StreamWriter(path, append, new UTF8Encoding(true));
            if (!hasContent)
            {
                writer.WriteLine("sep=;");
                writer.WriteLine(commaSeparatedHeader.Replace(',', ';'));
            }
            writer.Flush();
            return writer;
        }
        public static void WriteRow(TextWriter writer, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) writer.Write(';');
                writer.Write(Field(fields[i]));
            }
            writer.WriteLine();
        }

        // Preserve quoted commas, semicolons, quotes and multiline Logger messages.
        public static IEnumerable<string[]> ReadRows(string path)
        {
            using (var reader = new StreamReader(path))
            {
                var fields = new List<string>();
                var field = new StringBuilder();
                bool quoted = false, first = true;
                char delimiter = ',';
                int code;
                while ((code = reader.Read()) >= 0)
                {
                    char ch = (char)code;
                    if (ch == '"')
                    {
                        if (quoted && reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                        else quoted = !quoted;
                    }
                    else if (!quoted && ch == delimiter) { fields.Add(field.ToString()); field.Clear(); }
                    else if (!quoted && (ch == '\r' || ch == '\n'))
                    {
                        if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                        fields.Add(field.ToString()); field.Clear();
                        if (first && fields.Count == 1 && fields[0] == "sep=;") delimiter = ';';
                        else if (!(first && fields.Count == 2 && fields[0] == "sep=" && fields[1] == ""))
                            yield return fields.ToArray();
                        fields.Clear(); first = false;
                    }
                    else field.Append(ch);
                }
                if (quoted) throw new InvalidDataException("Unclosed CSV quote: " + path);
                if (field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); yield return fields.ToArray(); }
            }
        }
    }
}
