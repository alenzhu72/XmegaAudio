# XmegaAudio

Windows 实时麦克风处理应用 + Audio SDK 模板，面向直播/会议场景：

- 背景噪音抑制（门限 + 自适应噪声底）
- 人声电平（自动增益 + 限幅）
- 混响（房间大小/阻尼/Wet）
- EQ 曲线（频率轴拖拽）
- 全局快捷键（静音、播放音效等）
- 设置保存/加载（JSON）

## 快速开始（开发）

前置条件：
- Visual Studio 2022（建议 17.8+）
- .NET SDK 8.x

打开解决方案：
- `XmegaAudio.sln`

运行（命令行）：

```powershell
dotnet run --project .\XmegaAudio.csproj
```

## 仓库结构（模板）

- `Audio/`：Audio SDK 源码（音频采集、处理链路、效果器模块）
- `XmegaAudio.AudioSdk/`：Audio SDK 项目（复用 `Audio/` 源码）
- `XmegaAudio.Samples.Basic/`：示例代码（如何引用 SDK）
- `.github/workflows/release.yml`：打 Tag 自动编译并生成 Release

## Audio SDK 结构

SDK 以 `XmegaAudio.Audio` 命名空间提供核心能力：

- `AudioEngine`：设备枚举、Start/Stop、实时通路
- `ProcessingSettings`：处理参数
- `SampleProviders/*`：各效果器（Noise Gate、AutoGain、Reverb、EQ 等）
- `SoundEffectsPlayer`：音效播放（输出到指定设备）

应用层（WPF）只负责 UI、快捷键与配置持久化，音频算法与通路在 SDK 内复用。

## Release 自动编译

推送 Tag 触发 GitHub Actions 构建并生成 Release：

- Tag 规则：`v*`（例如 `v0.1.0`）
- 产物：`XmegaAudio-win-x64.zip`

## License

MIT，详见 `LICENSE`。

