# Called Assistant

一款基于 WPF 的 Windows 桌面 AI 语音/文字助手，通过全局热键唤醒，支持本地 Whisper 语音识别与多种 LLM 提供商。

## ✨ 功能

- **全局热键唤醒**：自定义快捷键（默认 `Alt+Space`）随时唤起悬浮窗
- **语音优先模式**：唤醒即开始录音，按 `Space` 停止并发送；再按 `Space` 开始新一轮对话
- **文字优先模式**：显示输入框，输入后按 `Enter` 发送
- **本地 Whisper STT**：离线语音识别，支持 Tiny / Base / Small / Medium / Large-v3 模型，设置页面一键下载
- **OpenAI Whisper API**：云端语音识别
- **LLM 支持**：Ollama（本地）、OpenAI、OpenAI 兼容接口
- **流式输出**：AI 回答实时逐字显示
- **系统托盘**：最小化到托盘，右键菜单快速访问

## 🚀 下载安装

前往 [Releases](../../releases) 页面下载最新版本的 `CalledAssistant-win-x64.zip`，解压后直接运行 `CalledAssistant.exe`，无需安装。

> **依赖**：需要 [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)（或使用 self-contained 发布版本）。

## 🛠️ 从源码构建

```bash
git clone https://github.com/Ber1276/CalledAssistant.git
cd CalledAssistant
dotnet build -c Release
```

## ⚙️ 配置

首次运行后右键托盘图标 → **设置**：

| 项目 | 说明 |
|------|------|
| 全局热键 | 修饰键 + 按键组合 |
| STT 提供商 | 本地 Whisper 或 OpenAI API |
| Whisper 模型 | 选择大小后点击"下载模型"自动下载到 `%AppData%\CalledAssistant\` |
| LLM 提供商 | Ollama / OpenAI / 自定义兼容接口 |

## 📦 发布流程

推送 `v*` 格式的 tag 即可自动触发 GitHub Actions 构建并发布 Release：

```bash
git tag v1.0.0
git push origin v1.0.0
```

## 📄 License

MIT
