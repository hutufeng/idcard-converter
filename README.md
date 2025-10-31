# IDCardOCR C# 项目

此目录包含身份证 OCR 和导出系统的 C# 实现。它包括一个控制台应用程序和一个 WPF (Windows Presentation Foundation) 桌面应用程序。

## 项目结构

- `IDCardOCR.sln`: Visual Studio 解决方案文件。
- `IDCardOCR.Console/`: 一个用于基本 OCR 测试的控制台应用程序（非主要焦点）。
- `IDCardOCR.WPF/`: 带有图形用户界面的主 WPF 桌面应用程序。

## 构建项目

要构建项目，请在 Visual Studio 中打开 `IDCardOCR.sln` 或使用 .NET CLI：

```bash
dotnet build IDCardOCR.sln --configuration Release
```

## 运行 WPF 应用程序

构建完成后，WPF 应用程序的可执行文件位于：

`csharp/IDCardOCR.WPF/bin/Release/net9.0-windows/IDCardOCR.WPF.exe`

## 生成绿色软件包（解压即用）

要创建一个自包含、可移植的软件包，该软件包可以立即解压和运行（无需在目标机器上安装 .NET 运行时），请使用以下命令：

```bash
dotnet publish IDCardOCR.WPF/IDCardOCR.WPF.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishReadyToRun=true -o publish_directory
```

这将生成一个包含所有必要文件的 `publish_directory`。然后，您可以压缩此目录（例如，使用 7-Zip 进行极致压缩）以进行分发。

## 主要功能

- **图像选择：** 选择单个图像文件或包含身份证图像的整个文件夹。
- **OCR 处理：** 利用 PaddleOCR 从身份证图像中提取文本。
- **身份证解析：** 从 OCR 结果中提取结构化信息（姓名、身份证号码、地址等）。
- **数据显示：** 在数据网格中显示解析后的身份证信息。
- **Excel 导出：** 将解析后的数据导出到 Excel 文件。

有关更详细的技术信息，请参阅此目录中的 `TECHNICAL_DOCUMENTATION.md`。