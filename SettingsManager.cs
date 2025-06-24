using System;
using System.Collections.Generic;
using System.IO;

namespace DismToolGui
{
    public static class SettingsManager
    {
        private static readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.config");

        public static bool GetBool(string key)
        {
            if (!File.Exists(configPath))
                return false;

            var lines = File.ReadAllLines(configPath);
            foreach (var line in lines)
            {
                if (line.StartsWith(key + "="))
                {
                    return line.Split('=')[1].Trim().ToLower() == "true";
                }
            }
            return false;
        }

        public static void Set(string key, string value)
        {
            Dictionary<string, string> settings = new();

            if (File.Exists(configPath))
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
                    if (line.Contains("="))
                    {
                        var parts = line.Split('=');
                        settings[parts[0]] = parts[1];
                    }
                }
            }

            settings[key] = value;

            using var writer = new StreamWriter(configPath);
            foreach (var kvp in settings)
            {
                writer.WriteLine($"{kvp.Key}={kvp.Value}");
            }
        }

        // ✅ Add this to fix CS0117 error
        public static void SetBool(string key, bool value)
        {
            Set(key, value ? "true" : "false");
        }
    }
}
