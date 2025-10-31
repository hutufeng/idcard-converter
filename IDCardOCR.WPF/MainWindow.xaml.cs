
using Microsoft.Win32;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;

using Sdcb.PaddleOCR.Models.Local;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OfficeOpenXml;

using System.Windows.Controls.Primitives;

namespace IDCardOCR.WPF
{
    public partial class MainWindow : System.Windows.Window
    {
        private PaddleOcrAll? _engine = null;
        private readonly ObservableCollection<FileListItem> _fileList = new ObservableCollection<FileListItem>();

        public ICommand RenameColumnCommand { get; private set; }
        public ICommand HideColumnCommand { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            FileListBox.ItemsSource = _fileList;
            ExcelPackage.License.SetNonCommercialPersonal("user");

            RenameColumnCommand = new RelayCommand(RenameColumnExecute);
            HideColumnCommand = new RelayCommand(HideColumnExecute);
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp)|*.png;*.jpeg;*.jpg;*.bmp|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    _fileList.Add(new FileListItem(fileName));
                }
            }
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] files = Directory.GetFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".jpg") || s.EndsWith(".png") || s.EndsWith(".jpeg") || s.EndsWith(".bmp")).ToArray();
                foreach (var file in files)
                {
                    _fileList.Add(new FileListItem(file));
                }
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FileListBox.SelectedItems.Cast<FileListItem>().ToList();
            foreach (var item in selectedItems)
            {
                _fileList.Remove(item);
            }
        }

        private void RenameFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItem is FileListItem selectedItem)
            {
                InputDialog dialog = new InputDialog("重命名文件", selectedItem.DisplayName);
                if (dialog.ShowDialog() == true)
                {
                    selectedItem.DisplayName = dialog.InputText;
                    // Refresh the ListBox to show the updated name
                    FileListBox.Items.Refresh();
                }
            }
            else
            {
                System.Windows.MessageBox.Show("请选择一个文件进行重命名。");
            }
        }

        private async void StartOcrButton_Click(object sender, RoutedEventArgs e)
        {
            OcrProgressBar.Value = 0;
            StatusTextBlock.Text = "开始识别...";
            try
            {
                await ProcessFiles(_fileList);
                StatusTextBlock.Text = "识别完成！";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "识别失败！";
                System.Windows.MessageBox.Show($"发生错误: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ProcessFiles(ObservableCollection<FileListItem> fileListItems)
        {

            try
            {
                _engine = new PaddleOcrAll(LocalFullModels.ChineseV5, PaddleDevice.Mkldnn());

                List<List<FileListItem>> groupedFiles = GroupFiles(fileListItems);
                List<IDCardInfo> idCardInfos = new List<IDCardInfo>();

                OcrProgressBar.Maximum = fileListItems.Count;
                OcrProgressBar.Value = 0;

                int processedImageCount = 0;
                foreach (var group in groupedFiles)
                {
                    StringBuilder combinedText = new StringBuilder();
                    foreach (var fileItem in group)
                    {
                        using (Mat src = Cv2.ImRead(fileItem.FilePath, ImreadModes.Color))
                        {
                            if (_engine is not null)
                            {
                                PaddleOcrResult result = await Task.Run(() => _engine.Run(src));
                                if (result.Regions.Any())
                                {
                                    foreach (var region in result.Regions)
                                    {
                                        combinedText.AppendLine(region.Text);
                                    }
                                }
                            }
                        }
                        processedImageCount++;
                        StatusTextBlock.Text = $"正在识别图像: {processedImageCount}/{fileListItems.Count}";
                        OcrProgressBar.Value = processedImageCount;
                        ProgressPercentageTextBlock.Text = $"{ (int)((double)processedImageCount / fileListItems.Count * 100) }% ";
                    }


                    IDCardInfo info = IDCardParser.Parse(combinedText.ToString());
                    info.序号 = idCardInfos.Count + 1;
                    idCardInfos.Add(info);
                }

                OcrResultDataGrid.ItemsSource = idCardInfos;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"OCR处理失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                _engine?.Dispose();
                _engine = null; // Set to null after disposing
            }
        }

        private List<List<FileListItem>> GroupFiles(ObservableCollection<FileListItem> fileListItems)
        {
            return fileListItems
                .Select(item => new { Item = item, BaseName = item.DisplayName.Split('_').First() })
                .GroupBy(x => x.BaseName)
                .Select(g => g.Select(x => x.Item).Take(2).ToList())
                .ToList();
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (OcrResultDataGrid.ItemsSource == null)
            {
                System.Windows.MessageBox.Show("没有数据可导出。");
                return;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
            if (saveFileDialog.ShowDialog() == true)
            {
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("OCR Results");
                    var items = (List<IDCardInfo>)OcrResultDataGrid.ItemsSource;

                    // Get visible columns and their headers
                    var visibleColumns = OcrResultDataGrid.Columns.Where(c => c.Visibility == Visibility.Visible).ToList();
                    for (int i = 0; i < visibleColumns.Count; i++)
                    {
                        worksheet.Cells[1, i + 1].Value = visibleColumns[i].Header.ToString();
                    }

                    // Populate data for visible columns
                    for (int row = 0; row < items.Count; row++)
                    {
                        for (int col = 0; col < visibleColumns.Count; col++)
                        {
                            var column = visibleColumns[col];
                            // Get the property name from the binding path
                            if (column is DataGridBoundColumn boundColumn)
                            {
                                if (boundColumn.Binding is System.Windows.Data.Binding binding && binding.Path != null)
                                {
                                    var propertyName = binding.Path.Path;
                                    var property = typeof(IDCardInfo).GetProperty(propertyName);
                                    if (property != null)
                                    {
                                        worksheet.Cells[row + 2, col + 1].Value = property.GetValue(items[row]);
                                    }
                                }
                            }
                            else if (column is DataGridTemplateColumn templateColumn && templateColumn.Header.ToString() == "原始OCR")
                            {
                                // For "原始OCR" column, export the RawOcrText
                                worksheet.Cells[row + 2, col + 1].Value = items[row].RawOcrText;
                            }
                        }
                    }

                    File.WriteAllBytes(saveFileDialog.FileName, package.GetAsByteArray());
                }
                System.Windows.MessageBox.Show($"数据已导出到 {saveFileDialog.FileName}");
            }
        }

        private void RenameColumnExecute(object? parameter)
        {
            DataGridColumnHeader? columnHeader = parameter as DataGridColumnHeader;
            if (columnHeader is null) return;

            DataGridColumn? column = columnHeader.Column;
            if (column is null) return;

            InputDialog dialog = new InputDialog("重命名列", column.Header?.ToString() ?? string.Empty);
            if (dialog.ShowDialog() == true)
            {
                column.Header = dialog.InputText;
            }
        }

        private void HideColumnExecute(object? parameter)
        {
            DataGridColumnHeader? columnHeader = parameter as DataGridColumnHeader;
            if (columnHeader is null) return;

            DataGridColumn? column = columnHeader.Column;
            if (column is null) return;

            column.Visibility = Visibility.Collapsed;
        }

        private void ColumnSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<ColumnSetting> currentSettings = new ObservableCollection<ColumnSetting>();
            foreach (var column in OcrResultDataGrid.Columns)
            {
                currentSettings.Add(new ColumnSetting
                {
                    OriginalHeader = column.Header.ToString(),
                    DisplayHeader = column.Header.ToString(),
                    IsVisible = column.Visibility == Visibility.Visible
                });
            }

            ColumnSettingsWindow settingsWindow = new ColumnSettingsWindow(currentSettings);
            if (settingsWindow.ShowDialog() == true)
            {
                // Apply new settings
                foreach (var newSetting in settingsWindow.ColumnSettings)
                {
                    var column = OcrResultDataGrid.Columns.FirstOrDefault(c => c.Header != null && c.Header.ToString() == newSetting.OriginalHeader);
                    if (column != null)
                    {
                        string? displayHeader = newSetting.DisplayHeader;
                        column.Header = (object?)displayHeader;
                        column.Visibility = newSetting.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private void ViewRawOcr_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is IDCardInfo selectedInfo)
            {
                RawOcrViewerDialog dialog = new RawOcrViewerDialog(selectedInfo);
                if (dialog.ShowDialog() == true)
                {
                    // Refresh the DataGrid to show updated info
                    OcrResultDataGrid.Items.Refresh();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _engine?.Dispose();
            base.OnClosed(e);
        }
    }

    public class FileListItem
    {
        public string DisplayName { get; set; }
        public string FilePath { get; set; }

        public FileListItem(string filePath)
        {
            FilePath = filePath;
            DisplayName = Path.GetFileName(filePath);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public enum FieldStatus
    {
        Success,
        Partial,
        Failed,
        NotFound,
        LowQuality
    }

    public class IDCardInfo
    {
        public int 序号 { get; set; }
        public string? 姓名 { get; set; }
        public FieldStatus 姓名Status { get; set; } = FieldStatus.NotFound;
        public string? 性别 { get; set; }
        public FieldStatus 性别Status { get; set; } = FieldStatus.NotFound;
        public string? 年龄 { get; set; }
        public FieldStatus 年龄Status { get; set; } = FieldStatus.NotFound;
        public string? 出生日期 { get; set; }
        public FieldStatus 出生日期Status { get; set; } = FieldStatus.NotFound;

        public string? 出生日期ExcelFormat
        {
            get
            {
                if (DateTime.TryParse(出生日期?.Replace("年", "-").Replace("月", "-").Replace("日", ""), out DateTime date))
                {
                    return date.ToString("yyyy-MM-dd");
                }
                return 出生日期; // Return original if parsing fails
            }
        }
        public string? 民族 { get; set; }
        public FieldStatus 民族Status { get; set; } = FieldStatus.NotFound;
        public string? 身份证号码 { get; set; }
        public FieldStatus 身份证号码Status { get; set; } = FieldStatus.NotFound;
        public string? 住址 { get; set; }
        public FieldStatus 住址Status { get; set; } = FieldStatus.NotFound;
        public string? 发行机关 { get; set; }
        public FieldStatus 发行机关Status { get; set; } = FieldStatus.NotFound;
        public string? 有效期 { get; set; }
        public FieldStatus 有效期Status { get; set; } = FieldStatus.NotFound;

        public string? RawOcrText { get; set; }
    }
}
