using System.Windows;

namespace IDCardOCR.WPF
{
    public partial class InputDialog : Window
    {
        public string InputText
        {
            get { return InputTextBox.Text; }
            set { InputTextBox.Text = value; }
        }

        public InputDialog(string title, string initialText = "")
        {
            InitializeComponent();
            Title = title;
            InputText = initialText;
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
}
