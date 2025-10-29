# 身份证照片信息提取导出系统 - 技术文档

## 1. 项目总览

本项目是一个基于 PySide6 和 RapidOCR 开发的桌面应用程序，旨在提供一个图形化界面，方便用户批量从身份证图片中提取结构化信息，并支持将结果导出为 Excel 文件。

该工具的核心是其强大且具有高容错性的信息提取引擎，能够应对各种不完美的 OCR 识别结果，最大限度地保证提取信息的准确性。

## 2. 功能特性

- **图形化用户界面**: 基于 PySide6 构建，界面简洁，操作直观。
- **文件/文件夹选择**: 支持通过对话框选择单个/多个图片文件，或直接选择整个文件夹进行处理。
- **多线程处理**: OCR 识别过程在独立的子线程中进行，避免主界面卡顿，保证流畅的用户体验。
- **实时进度反馈**:
    - 状态栏实时显示当前处理状态和总体进度百分比。
    - 进度条内嵌于状态栏右侧，清晰展示任务进程。
- **智能图像分组**: 自动将文件名相似的图片（如 `A_1.jpg` 和 `A_2.jpg`）归为一组，用于处理身份证的正反面。
- **高鲁棒性信息提取**: 核心功能，能够从不完美的 OCR 结果中提取准确信息（详见“核心逻辑详解”）。
- **结果表格化展示**:
    - 提取结果实时显示在主界面的表格中。
    - **状态高亮**: 根据信息完整度，用不同颜色高亮行背景：
        - **红色**: 识别失败。
        - **黄色**: 部分信息识别成功（例如，成功识别身份证号，但缺少地址等）。
        - **正常**: 所有关键信息均识别成功。
- **表格自定义**:
    - **动态显隐**: 支持通过右键单击表头，勾选/取消勾选来动态显示或隐藏任意列。
    - **列重命名**: 支持右键单击表头重命名列标题。
- **数据交互**:
    - **右键复制**: 在表格中右键单击，可将选中行的数据以制表符分隔的格式复制到剪贴板，方便粘贴到 Excel 等软件中。
    - **Excel 导出**: 一键将表格中的所有数据导出为 `.xlsx` 文件。
- **用户体验优化**:
    - **窗口记忆**: 程序会自动记住上次关闭时的窗口大小和位置。
    - **工具栏固定**: 主工具栏锁定，不可移动，防止误操作。

## 3. 项目结构

项目遵循标准的 Python 应用结构，将 UI、核心逻辑和工具函数分离，便于维护和扩展。

```text
src/
├── main.py              # 应用主入口，负责初始化环境和UI
├── app/                 # UI相关模块
│   ├── main_window.py   # 主窗口UI布局、信号与槽连接
│   └── table_model.py   # 表格数据模型，负责数据与QTableView的交互
├── core/                # 核心业务逻辑
│   ├── ocr.py           # OCR识别与信息提取的核心算法
│   ├── grouping.py      # 图片分组逻辑
│   ├── excel_export.py  # Excel导出逻辑
│   └── models.py        # 定义项目使用的数据结构 (AppState, IDCardRecord)
└── utils/               # 通用辅助函数
    ├── helpers.py       # 提供身份证号解析、日期格式化等功能
    └── encoding_fix.py  # 提供文本乱码修复功能

scripts/
└── build.py             # 用于打包应用的脚本

models/                  # 存储OCR模型文件

```

## 4. 核心逻辑详解

### 4.1. 信息提取 (`core/ocr.py` - `extract_info` 函数)

这是整个项目的灵魂。为了应对 OCR 引擎返回结果的各种不确定性，我们设计了一套复杂但健壮的“混合提取策略”。

**基本流程:**
1.  **数据预处理**: 将 OCR 引擎返回的原始对象 (`RapidOCROutput`) 解析，并组合成 `[边界框, 文本, 分数]` 的标准格式列表。同时，对所有文本调用 `fix_garbled_text` 进行乱码修复。
2.  **身份证号优先**: 在所有文本中，使用正则表达式 `\d{17}[\dXx]` 全局搜索最关键的身份证号码。这是最高优先级的操作。
3.  **信息派生**: 一旦找到合法的身份证号，立即调用 `get_info_from_id_number` 函数从中推算出**性别、年龄、出生日期**。程序将**不再**从文本中提取这三项信息，以身份证号为唯一标准，确保了准确性。
4.  **统一字段提取**: 遍历所有文本行，根据关键字启动提取，并应用不同的策略：
    - **“贪婪”模式 (Greedy)**: 用于**地址、签发机关**这类可能跨行的字段。一旦定位到起始关键字（如“住址”），程序会一直读取后续的文本行，直到遇到下一个字段的关键字（如“公民身份号码”）为止。
    - **“非贪婪”模式 (Non-Greedy)**: 用于**姓名、民族**这类必定在单行内的短字段。程序只在找到关键字的当前行提取信息，绝不读取下一行，避免将独立的“噪音”字符（如之前遇到的“W”）错误地追加到结果中。
5.  **模糊关键字定位**: 为了应对关键字被 OCR 识别错误的情况（如“住址”识别成“往址”），我们对不同字段采用不同的定位策略：
    - **精确匹配**: 用于“姓名”、“民族”、“住址”。只有当文本中包含完整的关键字时才触发提取。
    - **模糊匹配**: 用于“签发机关”、“有效期限”。只要文本中包含这几个字中的任意两个，就触发提取。
6.  **状态评估**: 所有提取步骤完成后，程序会检查 `name`, `id_number`, `address`, `issuing_authority`, `validity_period` 这几个关键字段是否都已成功填充，并据此将该条记录的状态设置为 `SUCCESS`, `PARTIAL` 或 `FAILED`。

### 4.2. 图像分组 (`core/grouping.py`)

该模块的逻辑相对简单，它通过去除文件名中的 `_` 及后续部分（如 `A_1.jpg` -> `A`），将主干部分相同的文件归为一组，用于后续的正反面信息合并。

### 4.3. 辅助函数 (`utils/helpers.py`)

- **`get_info_from_id_number`**: 一个强大的身份证号解析器，支持15/18位身份证，包含校验码验证，并能准确计算年龄。
- **`parse_validity_period`**: 一个智能的日期格式化函数。它能从包含“长期”或各种乱码（如 `.` 缺失、字母混入）的字符串中，提取出数字，并努力将其格式化为 `YYYY.MM.DD-YYYY.MM.DD` 或 `YYYY.MM.DD-长期` 的标准格式。

### 4.4. 编码修复 (`utils/encoding_fix.py`)

`fix_garbled_text` 函数尝试通过将文本编码为 `latin-1` 再解码为 `gbk` 来修复常见的乱码问题。这是一种启发式方法，旨在处理 OCR 结果中可能出现的特定编码错误。在处理已正确编码的 UTF-8 文本时，该函数会返回原始文本，不会造成损坏。

## 5. UI 实现 (`app/main_window.py`, `app/table_model.py`)

### 5.1. `MainWindow` (`app/main_window.py`)

*   **多线程处理**: 使用 `QThread` (`Worker` 类) 将耗时的 OCR 任务放到后台线程执行，确保主 UI 线程的响应性。通过信号 (`Signal`) 进行线程间通信，更新 UI 进度和结果。
*   **UI 布局**: 采用 `QMainWindow` 作为主窗口，包含 `QToolBar`、`QStatusBar`、`QListWidget` (文件列表) 和 `QTableView` (结果展示)。布局清晰，功能分区明确。
*   **文件操作**: 提供“选择文件”和“选择文件夹”功能，支持添加和移除待处理图像文件。
*   **表格交互**: `QTableView` 支持行选择、右键复制数据、表头右键菜单进行列的动态显示/隐藏和重命名，极大地增强了用户对结果的控制。
*   **设置保存**: 使用 `QSettings` 自动保存和恢复窗口的几何位置和大小，提升用户体验。

### 5.2. `RecordTableModel` (`app/table_model.py`)

*   **数据绑定**: 继承 `QAbstractTableModel`，实现了 `rowCount`、`columnCount`、`data`、`headerData` 等核心方法，将 `AppState` 中的 `IDCardRecord` 列表与 `QTableView` 进行有效绑定。
*   **状态高亮**: 根据 `IDCardRecord` 的 `status` 字段（`SUCCESS`, `PARTIAL`, `FAILED`），使用 `Qt.BackgroundRole` 为表格行设置不同的背景颜色，直观反馈识别结果。
*   **数据编辑**: `setData` 方法允许用户编辑表格中的部分字段，并包含基本的类型转换和字段保护机制。

## 6. 数据模型 (`core/models.py`)

*   **`ImageGroup`**: 数据类，表示一组相关的图像（如身份证正反面），包含 `group_id`、`image_paths` 和 `status`。通过 `__post_init__` 确保 `image_paths` 数量不超过 2。
*   **`IDCardRecord`**: 数据类，表示从身份证中提取出的结构化信息，包含姓名、身份证号、地址、有效期等详细字段，以及 `status` 和 `raw_ocr_output` 用于记录处理状态和原始 OCR 摘要。
*   **`AppState`**: 数据类，集中管理应用程序的整体状态，包括 `IDCardRecord` 列表和 `column_settings` (表格列的顺序和自定义名称)。

## 7. 模型管理与离线支持

本项目使用 `rapidocr` 进行 OCR 识别，而 `rapidocr` 依赖 `huggingface_hub` 来管理其模型文件。为了确保应用程序的“离线优先”原则，模型文件必须随应用程序一起打包，并且 `huggingface_hub` 必须被正确引导以使用这些本地打包的模型。

### 7.1. 模型打包

*   **`models/` 目录**: 项目根目录下的 `models/` 文件夹用于存放 `rapidocr` 所需的 OCR 模型文件（例如 `models--SWHL--RapidOCR` 目录及其内容）。这些文件通过 `scripts/build.py` 中的 `--add-data=models;models` 参数被 PyInstaller 打包到应用程序内部的 `_internal/models` 路径下。
*   **`default_models.yaml` 和 `config.yaml`**: `rapidocr` 库还需要 `default_models.yaml` 和 `config.yaml` 等配置文件。这些文件通过 `--add-data` 参数被精确地放置在打包后的 `_internal/rapidocr/` 及其子目录中，以确保 `omegaconf` 能够正确加载它们。

### 7.2. `HF_HOME` 环境变量

在 `src/__main__.py` 中，当应用程序被 PyInstaller 打包并冻结时，会动态设置 `HF_HOME` 环境变量：

```python
if getattr(sys, 'frozen', False):
    application_path = sys._MEIPASS
    # ...
    os.environ['HF_HOME'] = os.path.join(application_path, 'models')
```

这将 `huggingface_hub` 的模型缓存目录指向 PyInstaller 解包后的临时目录 (`sys._MEIPASS`) 中的 `models` 文件夹。这样，`huggingface_hub` 会优先从这个本地路径加载模型，而不会尝试从网络下载，从而实现了离线功能。

### 7.3. 离线运行

通过上述打包和配置，应用程序在运行时将完全使用本地打包的模型文件，无需任何网络连接即可进行 OCR 识别，符合“离线优先”的核心原则。

## 8. 依赖项

主要依赖项（来自 `requirements.txt`）包括：

-   `PySide6`: UI 框架。
-   `openpyxl`: Excel 文件读写。
-   `pytest`: 单元测试框架（尽管测试文件已被删除）。
-   `ruff`: 代码风格与质量检查工具。
-   `rapidocr`: OCR 核心库。
-   `onnxruntime`: OCR 推理引擎。
-   `huggingface_hub`: 用于下载和管理 OCR 模型。
-   `pyinstaller`: 应用程序打包工具。

## 9. 代码质量 (`ruff.toml`)

`ruff.toml` 配置了 `ruff` 工具，启用了 `E` (Error)、`F` (Flake8)、`W` (Warning) 和 `I` (isort) 规则，确保了代码的基本质量和风格一致性。

## 10. 环境与启动

本项目支持在 Conda 环境或标准的 Python 虚拟环境（venv）中运行。

1.  **创建 Conda 环境**:
    ```bash
    conda create -n idcard_ocr python=3.11 -y
    ```
2.  **激活 Conda 环境**:
    ```bash
    conda activate idcard_ocr
    ```
3.  **创建并激活 Python 虚拟环境 (venv)**:
    ```bash
    python -m venv venv
    # Windows
    .\venv\Scripts\activate
    # Linux / macOS
    # source venv/bin/activate
    ```
4.  **安装依赖**:
    ```bash
    pip install -r requirements.txt
    ```

**重要提示：模型文件处理**

在开发模式下（非打包状态），如果 `models/` 目录中没有放置模型文件，`rapidocr` 在首次运行时会通过 `huggingface_hub` 自动从网络下载所需模型到 `~/.cache/huggingface/hub` 目录。**此过程需要互联网连接。** 为确保应用程序完全离线运行，建议您手动下载 `rapidocr` 所需的模型文件，并将其放置在项目根目录下的 `models/` 文件夹中。

5.  **运行程序 (开发模式)**:
    ```bash
    python src/__main__.py
    ```
6.  **打包程序**:
    ```bash
    python scripts/build.py
    ```
    打包后的可执行文件位于 `dist/IDCardOCRApp/IDCardOCRApp.exe`。