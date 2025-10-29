# 身份证照片信息提取导出系统

## 项目简介

本项目是一个基于 PySide6 和 RapidOCR 开发的桌面应用程序，旨在提供一个图形化界面，方便用户批量从身份证图片中提取结构化信息，并支持将结果导出为 Excel 文件。该工具的核心是其强大且具有高容错性的信息提取引擎，能够应对各种不完美的 OCR 识别结果，最大限度地保证提取信息的准确性。

## 主要功能

-   **图形化用户界面**: 简洁直观，易于上手。
-   **文件/文件夹选择**: 支持批量处理图片文件。
-   **多线程处理**: OCR 识别在后台运行，UI 响应流畅。
-   **实时进度反馈**: 状态栏和进度条清晰展示任务进程。
-   **智能图像分组**: 自动识别身份证正反面并分组。
-   **高鲁棒性信息提取**: 从不完美的 OCR 结果中提取准确信息。
-   **结果表格化展示**: 实时显示提取结果，并根据信息完整度高亮显示。
-   **表格自定义**: 支持动态显示/隐藏和重命名列。
-   **数据交互**: 支持右键复制选中行数据，一键导出为 Excel 文件。
-   **用户体验优化**: 记忆窗口大小位置，工具栏固定。

## 技术栈

-   **Python 3.11**
-   **PySide6**: UI 框架
-   **RapidOCR**: OCR 核心库
-   **openpyxl**: Excel 文件读写
-   **onnxruntime**: OCR 推理引擎
-   **huggingface_hub**: 用于下载和管理 OCR 模型
-   **PyInstaller**: 应用程序打包工具

## 安装与运行 (开发模式)

本项目支持在 Conda 环境或标准的 Python 虚拟环境（venv）中运行。

### 1. 克隆仓库

```bash
git clone [您的仓库地址]
cd idcard
```

### 2. 环境设置

#### 选项 A: 使用 Conda 环境 (推荐)

```bash
conda create -n idcard_ocr python=3.11 -y
conda activate idcard_ocr
```

#### 选项 B: 使用 Python 虚拟环境 (venv)

```bash
python -m venv venv
# Windows
.\venv\Scripts\activate
# Linux / macOS
# source venv/bin/activate
```

### 3. 安装依赖

```bash
pip install -r requirements.txt
```

**重要提示：模型文件处理**

`pip install` 命令只会安装 `rapidocr` 和 `huggingface_hub` 等库，**不会**直接下载 OCR 模型文件。模型文件（通常较大，例如 `.onnx` 文件）的处理方式如下：

*   **开发模式首次运行:** 如果您在 `models/` 目录中没有放置模型文件，`rapidocr` 在首次运行时会通过 `huggingface_hub` 自动从网络下载所需模型到 `~/.cache/huggingface/hub` 目录。**此过程需要互联网连接。**
*   **离线使用:** 为确保应用程序完全离线运行，建议您手动下载 `rapidocr` 所需的模型文件，并将其放置在项目根目录下的 `models/` 文件夹中。这样，程序将直接使用本地模型而无需下载。

### 4. 运行应用程序

```bash
python src/__main__.py
```

## 打包应用程序

要将应用程序打包为可执行文件，请运行项目根目录下的 `scripts/build.py` 脚本：

```bash
python scripts/build.py
```

打包完成后，可执行文件将位于 `dist/IDCardOCRApp/IDCardOCRApp.exe`。

## 项目结构

```
.  
├── DOCUMENTATION.md       # 详细技术文档  
├── GEMINI.md              # Gemini Agent 上下文和指南  
├── README.md              # 项目说明和快速开始  
├── requirements.txt       # Python 依赖列表  
├── ruff.toml              # Ruff 代码检查配置  
├── scripts/  
│   └── build.py           # 打包脚本  
├── src/  
│   ├── __main__.py        # 应用主入口  
│   ├── app/               # UI 相关模块  
│   │   ├── main_window.py  
│   │   └── table_model.py  
│   ├── core/              # 核心业务逻辑  
│   │   ├── ocr.py  
│   │   ├── grouping.py  
│   │   ├── excel_export.py  
│   │   └── models.py  
│   └── utils/             # 通用辅助函数  
│       ├── helpers.py  
│       └── encoding_fix.py  
└── models/                # 存储OCR模型文件  
```

## 许可证

[请在此处填写您的许可证信息，例如 MIT License]
