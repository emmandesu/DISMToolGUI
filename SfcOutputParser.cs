using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DismToolGui
{
    internal static class SfcOutputParser
    {
        private static readonly Regex PercentagePattern = new Regex(
            @"(?<percentage>\d{1,3})\s*%",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryParse(
            string rawOutput,
            out string message,
            out int? progressPercentage)
        {
            message = string.Empty;
            progressPercentage = null;

            if (string.IsNullOrEmpty(rawOutput))
                return false;

            string printableOutput = RemoveControlCharacters(rawOutput);
            MatchCollection percentageMatches = PercentagePattern.Matches(printableOutput);
            if (percentageMatches.Count > 0 &&
                int.TryParse(
                    percentageMatches[percentageMatches.Count - 1].Groups["percentage"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int percentage))
            {
                progressPercentage = Math.Max(0, Math.Min(100, percentage));
                return true;
            }

            message = NormalizeConsoleText(rawOutput).Trim();
            return message.Length > 0;
        }

        private static string RemoveControlCharacters(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (!char.IsControl(character))
                    result.Append(character);
            }

            return result.ToString();
        }

        private static string NormalizeConsoleText(string value)
        {
            var result = new StringBuilder(value.Length);
            int cursor = 0;

            foreach (char character in value)
            {
                switch (character)
                {
                    case '\r':
                        result.Clear();
                        cursor = 0;
                        break;

                    case '\b':
                        if (cursor > 0)
                            cursor--;
                        break;

                    case '\t':
                        WriteCharacter(result, ref cursor, ' ');
                        break;

                    default:
                        if (!char.IsControl(character))
                            WriteCharacter(result, ref cursor, character);
                        break;
                }
            }

            return result.ToString();
        }

        private static void WriteCharacter(
            StringBuilder result,
            ref int cursor,
            char character)
        {
            if (cursor < result.Length)
                result[cursor] = character;
            else
                result.Append(character);

            cursor++;
        }
    }
}
