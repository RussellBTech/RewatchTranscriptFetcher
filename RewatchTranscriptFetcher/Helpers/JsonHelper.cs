using System.IO;
using Newtonsoft.Json;
using RewatchTranscriptFetcher.Models;

namespace RewatchTranscriptFetcher.Helpers
{
    public static class JsonHelper
    {
        private static readonly string SettingsFilePath = "rewatchSettings.json";

        public static RewatchSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
                return null;

            var json = File.ReadAllText(SettingsFilePath);
            return JsonConvert.DeserializeObject<RewatchSettings>(json);
        }

        public static void SaveSettings(RewatchSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
