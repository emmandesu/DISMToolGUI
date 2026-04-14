using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DismToolGui
{
    public static class SettingsManager
    {
        private static readonly string configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DismToolGui");

        private static readonly string configPath = Path.Combine(configDirectory, "settings.config");

        public static bool GetBool(string key)
        {
            string value = Get(key);
            return bool.TryParse(value, out bool result) && result;
        }

        public static string Get(string key, string defaultValue = "")
        {
            try
            {
                if (!File.Exists(configPath))
                    return defaultValue;

                foreach (var line in File.ReadAllLines(configPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains('='))
                        continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    string currentKey = parts[0].Trim();

                    if (string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                        return parts.Length > 1 ? parts[1].Trim() : defaultValue;
                }
            }
            catch
            {
                // Ignore read failures and return default
            }

            return defaultValue;
        }

        public static void Set(string key, string value)
        {
            Directory.CreateDirectory(configDirectory);

            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(configPath))
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains('='))
                        continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    string existingKey = parts[0].Trim();
                    string existingValue = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                    settings[existingKey] = existingValue;
                }
            }

            settings[key] = value ?? string.Empty;

            using var writer = new StreamWriter(configPath, false);
            foreach (var kvp in settings)
            {
                writer.WriteLine($"{kvp.Key}={kvp.Value}");
            }
        }

        public static void SetBool(string key, bool value)
        {
            Set(key, value.ToString().ToLowerInvariant());
        }
    }
}
