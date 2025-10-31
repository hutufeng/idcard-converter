using System.Collections.ObjectModel;
using System.Windows;

namespace IDCardOCR.WPF
{
    public partial class ColumnSettingsWindow : Window
    {
        public ObservableCollection<ColumnSetting> ColumnSettings { get; set; }

        public ColumnSettingsWindow(ObservableCollection<ColumnSetting> currentSettings)
        {
            InitializeComponent();
            ColumnSettings = new ObservableCollection<ColumnSetting>(currentSettings);
            DataContext = this;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    public class ColumnSetting
    {
        public string? OriginalHeader { get; set; }
        public string? DisplayHeader { get; set; }
        public bool IsVisible { get; set; }
    }
}
