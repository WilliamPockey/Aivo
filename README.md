# 🤖 Aivo: 基于 Unity 的 AI 驱动虚拟人桌宠 & 智能助手

<div align="center">
  <img src="https://img.shields.io/badge/Unity-2021.3+-blue.svg?style=for-the-badge&logo=unity" alt="Unity Version">
  <img src="https://img.shields.io/badge/Language-C%23-green.svg?style=for-the-badge" alt="C#">
  <img src="https://img.shields.io/badge/Backend-Python-yellow.svg?style=for-the-badge&logo=python" alt="Python">
  <img src="https://img.shields.io/badge/License-MIT-red.svg?style=for-the-badge" alt="License">
</div>

---

**Aivo** 是一款集成了大语言模型（LLM）、多模态视觉感知、流式语音合成（TTS）以及 3D 虚拟人驱动的开源桌面助手。它不仅是你的桌面萌宠，更是能够感知你的工作环境、管理文件并进行深度交互的智能 Agent。

---

## 📺 项目演示 (Main Demo)

<div align="center">
  <video src="https://pockey.space/media/普通交互-1.mp4" width="800" controls title="Aivo 基础交互演示"></video>
  <p><i>基础对话与实时交互演示</i></p>
</div>

---

## ✨ 核心功能演示

### 🧠 AI 与 Agent 深度能力
| 功能模块 | 演示视频 (点击播放) | 功能描述 |
| :--- | :--- | :--- |
| **RAG 知识库** | [🎬 观看视频](https://pockey.space/media/RAG-1.mp4) | 基于本地文档的检索增强生成 |
| **联网搜索** | [🎬 观看视频](https://pockey.space/media/联网搜索-1.mp4) | 实时接入互联网获取最新资讯 |
| **长期记忆** | [🎬 观看视频](https://pockey.space/media/对话历史长期记忆-1.mp4) | 跨Session的对话上下文保持 |
| **工作目录感知** | [🎬 观看视频](https://pockey.space/media/工作目录感知-1.mp4) | AI 实时感知并理解你的本地项目文件 |

### 🎭 交互与多模态
| 功能模块 | 演示视频 (点击播放) | 功能描述 |
| :--- | :--- | :--- |
| **多模态交互** | [🎬 观看视频](https://pockey.space/media/多模态交互-1.mp4) | 发送图片给虚拟人，实现视觉理解 |
| **文件操作** | [🎬 观看视频](https://pockey.space/media/文件操作-1.mp4) | 通过对话指令让 AI 整理或移动文件 |
| **自定义配置** | [🎬 观看视频](https://pockey.space/media/模型切换与自定义配置-1.mp4) | 运行时切换 VRM 模型与后端 API 地址 |
| **历史记录** | [🎬 观看视频](https://pockey.space/media/历史记录-1.mp4) | 完整的对话日志与回溯功能 |

---

## ✨ 核心功能

### 🎭 3D 表现与交互
* **VRM 原生支持**：支持 `.vrm` 格式模型动态加载，具备完整的骨骼驱动与表情控制。
* **智能备份系统**：上传的新模型会自动保存至工作目录的 `Models` 文件夹，实现模型库的持久化管理。
* **uLipSync 口型同步**：通过实时音频频谱分析，驱动虚拟人精确的口型变化。
* **随机待机逻辑**：内置多种 Idle 动画与随机动作触发，告别死板。

### 🧠 AI 驱动能力
* **流式长对话**：基于 WebSocket 的双工通信，支持 AI 边生成边播放（语音+文本）。
* **Agent 工作流**：一键切换 **Agent 模式**，AI 可访问专属工作目录，执行文件检索与整理任务。
* **多模态感知**：支持发送本地图片，AI 可理解图片内容并针对画面进行交互式对话。
* **文件拖拽直达**：支持将文件从 Windows 资源管理器直接拖拽至虚拟人，自动同步至 Agent 空间。

### ⚙️ 高度可定制
* **角色设置面板**：UI 内置模型切换、LLM/TTS 地址配置、系统提示词（System Prompt）实时修改。
* **性别动画适配**：针对不同角色可动态切换男/女动画控制器。

---

## 🎁 所有功能

| 前端需求 | 后端需求 |
| :--- | :--- |
| 桌宠模式 ✅ | 大模型API ✅ |
| 文字输入 ✅ | TTS API ✅ |
| 单次对话历史记录 ✅ | 日程设置与主动提醒 ✅（基于Outlook实现，使用必须安装MicroSoft 365） |
| 两套性别的人物动画 ✅ | 网页爬取、分析 ✅（无URL进行搜索时必须开启代理） |
| 语音输入 ✅  | ~~GIT管理~~ 智能文件管理 ✅ |
| 人物切换/定制（支持.vrm模型，用户可通过Vroid Studio或资源网站定制） ✅ | OCR（中英日） ✅（通过StreamingAssets导入Tessract内置） |
| LLM API设置、TTS API设置、提示词设置、音频设置 ✅ | 简易本地项目/笔记管理（RAG） ✅ |
| Agent模式切换与设置 ✅ | 长期记忆 ✅（对话记忆、用户画像绘制） |
| 图片输入与多模态大模型交互 ✅ | 文件拖拽钩子（直接将文件拖入工作文件夹） ✅ |


---

## 🛠️ 技术架构

### 核心组件说明 (Unity Frontend)
* **`ChatManager.cs`**: 系统的通信中枢。处理 WebSocket 消息队列、UI 状态机、以及图片 Base64 转换。
* **`CharacterLoader.cs`**: 模型加载中心。负责扫描 `Models` 文件夹、异步加载渲染、以及自动计算物理碰撞体。
* **`GlobalSettings.cs`**: 配置持久化层。通过 `PlayerPrefs` 记录用户偏好、工作路径及 API 地址。
* **`WindowsFileBrowser.cs`**: 调用原生 `comdlg32.dll` 实现非阻塞的文件选择体验。

### 后端技术 (Python Backend)
* **通信层**: 基于 `FastAPI` / `WebSockets` 实现高并发连接。
* **AI 核心**: 集成主流 LLM API (如 Qwen-VL, Llama) 以及语音合成引擎。

---

## 🚀 快速开始

### 前置要求
* Unity 2021.3 LTS 或更高版本。
* Python 3.9+。
* 支持 VRM 的插件：UniVRM。

### 安装步骤
1.  **克隆项目**
    ```bash
    git clone [https://github.com/YourUsername/Aivo.git](https://github.com/YourUsername/Aivo.git)
    ```
2.  **配置后端**
    * 进入 `/Server` 文件夹。
    * 安装依赖：`pip install -r requirements.txt`。
    * 运行服务端：`python main.py`。
3.  **运行前端**
    * 在 Unity 中打开项目。
    * 进入设置面板，配置 `LLM URL` 与 `Agent 工作路径`。
    * 点击 **Connect** 即可开始交流。

---

## 📅 开发历程

这个项目起源于对“拥有灵魂的桌宠”的探索。从最初的简单文字对话，到后来克服了 **VRM 运行时动态加载** 的技术难题，再到接入 **多模态视觉分析**，每一个功能的迭代都凝聚了对交互体验的追求。特别是在处理 **Agent 文件管理** 时，我们实现了本地文件系统与 AI 逻辑的深度闭环。

---

## 🤝 贡献与感谢

感谢在开发过程中提供支持的所有开源社区工具：
- [UniVRM](https://github.com/vrm-c/UniVRM)
- [uLipSync](https://github.com/hecomi/uLipSync)

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
