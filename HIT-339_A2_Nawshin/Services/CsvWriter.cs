using System.Text;

namespace Bibliotheca.Services
{
    /// <summary>
    /// Builds CSV files that open cleanly in Excel (UTF-8 with BOM, CRLF line endings).
    /// Values that could be run as spreadsheet formulas (=, +, @ ...) are prefixed with an
    /// apostrophe so imported text can never become a formula when a report is opened.
    /// </summary>
    public static class CsvWriter
    {
        public static byte[] ToBytes(IEnumerable<IEnumerable<string>> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                sb.Append(string.Join(",", row.Select(Escape)));
                sb.Append("\r\n");
            }

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return encoding.GetPreamble().Concat(encoding.GetBytes(sb.ToString())).ToArray();
        }

        public static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = value;
            if (LooksLikeFormula(text))
            {
                text = "'" + text;
            }

            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                text = "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            return text;
        }

        private static bool LooksLikeFormula(string text)
        {
            var first = text[0];
            if (first == '=' || first == '+' || first == '@' || first == '\t' || first == '\r')
            {
                return true;
            }

            // A leading minus is fine for numbers such as -12.50 or -$12.50, but not for text like -cmd.
            if (first == '-' && text.Length > 1)
            {
                var second = text[1];
                return !(char.IsDigit(second) || second == '.' || second == '$');
            }

            return false;
        }
    }
}
