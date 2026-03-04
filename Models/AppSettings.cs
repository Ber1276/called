using System.Text.Json.Serialization;

namespace CalledAssistant.Models
{
    public class AppSettings
    {
        // 热键配置
        public HotkeyConfig Hotkey { get; set; } = new HotkeyConfig();

        // 输入优先模式
        public InputMode InputMode { get; set; } = InputMode.Voice;

        // STT 配置
        public SttSettings Stt { get; set; } = new SttSettings();

        // LLM 配置
        public LlmSettings Llm { get; set; } = new LlmSettings();
    }

    public class HotkeyConfig
    {
        public bool Alt { get; set; } = true;
        public bool Ctrl { get; set; } = false;
        public bool Shift { get; set; } = false;
        public uint VirtualKey { get; set; } = 0x20; // Space

        public string DisplayText
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (Ctrl) parts.Add("Ctrl");
                if (Alt) parts.Add("Alt");
                if (Shift) parts.Add("Shift");
                parts.Add(KeyDisplayName);
                return string.Join("+", parts);
            }
        }

        private string KeyDisplayName => VirtualKey switch
        {
            0x20 => "Space",
            0x70 => "F1",
            0x71 => "F2",
            0x72 => "F3",
            0x73 => "F4",
            0x74 => "F5",
            0x75 => "F6",
            0x76 => "F7",
            0x77 => "F8",
            0x78 => "F9",
            0x79 => "F10",
            0x7A => "F11",
            0x7B => "F12",
            _ => ((char)VirtualKey).ToString()
        };
    }

    public enum InputMode
    {
        Voice,
        Text
    }

    public class SttSettings
    {
        public SttProvider Provider { get; set; } = SttProvider.LocalWhisper;
        public string ModelPath { get; set; } = string.Empty;
        public WhisperModelSize ModelSize { get; set; } = WhisperModelSize.Base;
        public string Language { get; set; } = "zh";
        public string ApiKey { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
    }

    public enum SttProvider
    {
        LocalWhisper,
        OpenAiWhisper
    }

    public enum WhisperModelSize
    {
        Tiny,
        Base,
        Small,
        Medium,
        LargeV3
    }

    public class LlmSettings
    {
        public LlmProvider Provider { get; set; } = LlmProvider.Ollama;
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string ApiKey { get; set; } = string.Empty;
        public string ModelName { get; set; } = "llama3.2";
        public string SystemPrompt { get; set; } = "You are a helpful assistant. Answer concisely.";
    }

    public enum LlmProvider
    {
        Ollama,
        OpenAI,
        OpenAICompatible
    }
}
