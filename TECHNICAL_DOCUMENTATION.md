# IDCardOCR C# 项目技术文档

## 1. 项目概述

本项目旨在提供一个基于 C# 和 WPF 的身份证信息提取与导出系统。它利用 PaddleOCR 技术从身份证图片中识别文字信息，并对识别结果进行结构化解析、验证和后处理，最终支持将解析后的数据导出为 Excel 文件。系统设计为绿色软件包，解压即可运行，方便用户部署和使用。

## 2. 项目结构

```
csharp/
├───IDCardOCR.sln             # Visual Studio 解决方案文件
├───IDCardOCR.Console/        # 控制台应用程序（用于测试或命令行操作，非主要UI）
│   ├───IDCardOCR.Console.csproj
│   └───Program.cs
└───IDCardOCR.WPF/            # WPF 桌面应用程序（主用户界面）
    ├───App.xaml              # 应用程序定义
    ├───App.xaml.cs           # 应用程序主类
    ├───AssemblyInfo.cs       # 程序集信息
    ├───ColumnSettingsWindow.xaml # 列设置窗口 UI
    ├───ColumnSettingsWindow.xaml.cs # 列设置窗口逻辑
    ├───FieldStatusToColorConverter.cs # 字段状态到颜色转换器
    ├───IDCardOCR.WPF.csproj  # WPF 项目文件
    ├───IDCardParser.cs       # 身份证信息解析核心逻辑
    ├───InputDialog.xaml      # 通用输入对话框 UI
    ├───InputDialog.xaml.cs   # 通用输入对话框逻辑
    ├───MainWindow.xaml       # 主窗口 UI
    ├───MainWindow.xaml.cs    # 主窗口逻辑
    ├───RawOcrViewerDialog.xaml # 原始 OCR 结果查看器 UI
    ├───RawOcrViewerDialog.xaml.cs # 原始 OCR 结果查看器逻辑
    └───RelayCommand.cs       # 实现 ICommand 接口的辅助类
```

## 3. 核心组件与功能

### 3.1 `MainWindow.xaml.cs` (主窗口逻辑)

- **文件/文件夹选择：** 允许用户选择单个或批量身份证图片进行处理。
- **OCR 引擎初始化：** 使用 `Sdcb.PaddleOCR` 库初始化 `PaddleOcrAll` 引擎。**注意：** 引擎在每次处理文件时重新初始化，这是为了解决 `PaddleOcrAll` 在连续运行时可能出现的稳定性问题。
- **文件分组：** `GroupFiles` 方法根据文件名约定（例如 `ID_Front.jpg` 和 `ID_Back.jpg`）将图片分组，以便进行双面身份证识别。
- **OCR 处理流程：** 异步调用 `_engine.Run(src)` 执行 OCR 识别，并将结果合并。
- **数据解析：** 调用 `IDCardParser.Parse` 方法对 OCR 结果进行结构化解析。
- **数据显示：** 将解析后的 `IDCardInfo` 对象绑定到 `OcrResultDataGrid` 进行显示。
- **Excel 导出：** 支持将数据网格中的内容导出为 Excel 文件，并根据可见列进行导出。
- **日志记录：** 移除了日志记录功能，以减小包体积和简化部署。

### 3.2 `IDCardParser.cs` (身份证信息解析核心逻辑)

- **正则表达式：** 使用 `static readonly` 预编译的正则表达式模式，高效地进行文本匹配和提取。
- **关键字标准化：** `NormalizeKeywords` 方法用于纠正 OCR 结果中常见的关键字错误（例如“茶发机关”纠正为“签发机关”）。
- **身份证号码提取与验证：** `ExtractIdNumber` 方法优先提取身份证号码，并进行基本格式验证。包含对常见 OCR 错误的后处理（如 'l' 替换为 '1'）。
- **字段提取：** `ExtractField` 方法通过关键字和正则表达式模式从 OCR 文本中提取姓名、民族、住址、签发机关、有效期限等字段。它考虑了贪婪匹配和多行文本的情况。
- **字段验证：** `ValidateField` 方法对提取的各个字段进行类型特定的验证，判断其质量（例如姓名是否包含中文字符，有效期格式是否正确）。
- **字段后处理：** `CleanFieldSpecificNoise` 和 `PostProcessField` 方法对提取的字段进行进一步的清洗和格式化，例如去除无关字符、统一格式。
- **派生信息：** `GetBirthDateFromID`、`GetGenderFromID`、`GetAgeFromID` 方法从身份证号码中派生出生日期、性别和年龄信息。

## 4. OCR 引擎 (`Sdcb.PaddleOCR`)

本项目使用 `Sdcb.PaddleOCR` 库作为 OCR 引擎，它基于 PaddlePaddle 深度学习框架。在 `MainWindow.xaml.cs` 中，引擎通过 `new PaddleOcrAll(LocalFullModels.ChineseV5, PaddleDevice.Mkldnn())` 进行初始化，使用本地的中文模型和 Mkldnn 设备（CPU 优化）。

## 5. 数据流

1.  **图片选择：** 用户通过 UI 选择身份证图片。
2.  **图片分组：** 选定的图片根据文件名约定进行分组（例如，同一身份证的正反面）。
3.  **OCR 识别：** 对每张图片执行 PaddleOCR 识别，获取原始文本结果。
4.  **文本合并：** 同一组图片（例如正反面）的 OCR 文本被合并。
5.  **信息解析：** 合并后的文本通过 `IDCardParser` 进行结构化解析，提取各项身份证信息。
6.  **数据显示：** 解析后的信息在数据网格中展示。
7.  **数据导出：** 用户可将数据显示导出为 Excel 文件。

## 6. 构建与发布（绿色软件包）

为了生成一个解压即用的绿色软件包，请使用以下 `dotnet publish` 命令：

```bash
dotnet publish IDCardOCR.WPF/IDCardOCR.WPF.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishReadyToRun=true -o publish_directory
```

- `--configuration Release`: 以发布模式构建。
- `--runtime win-x64`: 指定目标运行时为 64 位 Windows。
- `--self-contained true`: 使应用程序自包含 .NET 运行时，无需目标机器预装 .NET。
- `-p:PublishReadyToRun=true`: 启用 ReadyToRun 编译，将应用程序代码预编译为本机代码，以提高启动性能。
- `-o publish_directory`: 指定输出目录为 `publish_directory`。

发布完成后，`publish_directory` 包含所有必要的文件。您可以进一步使用 7-Zip 等工具对 `publish_directory` 进行极致压缩，以减小分发包的大小。

## 7. 已知问题与变通方法

- **OCR 引擎连续运行稳定性：** `PaddleOcrAll` 引擎在连续处理大量图片或多次调用后可能出现稳定性问题。目前的解决方案是在每次处理文件批次时重新初始化引擎，以确保稳定性。这可能会带来一定的性能开销，但保证了功能的可靠性。
- **裁剪 (Trimming) 不兼容：** 由于 WPF 应用程序的特性，启用 .NET 的裁剪功能 (`-p:PublishTrimmed=true`) 可能会导致运行时错误，因此不建议使用。
