using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalledAssistant.Services
{
    public class SettingsService
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CalledAssistant");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public Models.AppSettings Current { get; private set; } = new Models.AppSettings();

        public SettingsService()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    Current = JsonSerializer.Deserialize<Models.AppSettings>(json, JsonOptions)
                              ?? new Models.AppSettings();
                }
            }
            catch
            {
                Current = new Models.AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Save failed: {ex.Message}");
            }
        }

        public string GetModelPath()
        {
            if (!string.IsNullOrWhiteSpace(Current.Stt.ModelPath) && File.Exists(Current.Stt.ModelPath))
                return Current.Stt.ModelPath;

            // 默认放到 AppData 目录
            var size = Current.Stt.ModelSize switch
            {
                Models.WhisperModelSize.Tiny => "tiny",
                Models.WhisperModelSize.Small => "small",
                Models.WhisperModelSize.Medium => "medium",
                Models.WhisperModelSize.LargeV3 => "large-v3",
                _ => "base"
            };
            return Path.Combine(SettingsDir, $"ggml-{size}.bin");
        }
    }
}
