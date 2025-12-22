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

## 📺 项目演示

> [!TIP]
> **请在此处上传你的演示视频。** > 你可以录制一段包含：对话交互、切换 Agent 模式、拖拽文件、以及更换 VRM 模型的完整演示。

<div align="center">
  <a href="你的视频链接">
    <img src="https://via.placeholder.com/800x450.png?text=Click+to+Watch+Aivo+Demo+Video" alt="Aivo Video Demo" width="700">
  </a>
</div>

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
