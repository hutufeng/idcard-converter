using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;

namespace IDCardOCR.WPF
{
    public partial class RawOcrViewerDialog : Window
    {
        public IDCardInfo CurrentIDCardInfo { get; private set; }

        public RawOcrViewerDialog(IDCardInfo idCardInfo)
        {
            InitializeComponent();
            CurrentIDCardInfo = idCardInfo;
            
            // Display raw OCR text
            RawOcrTextBox.Text = idCardInfo.RawOcrText;

            // Dynamically create editable fields for IDCardInfo properties
            PopulateEditFields();
        }

        private void PopulateEditFields()
        {
            foreach (PropertyInfo prop in typeof(IDCardInfo).GetProperties())
            {
                // Exclude '序号', 'RawOcrText', and status properties
                if (prop.Name == "序号" || prop.Name == "RawOcrText" || prop.Name.EndsWith("Status"))
                {
                    continue;
                }

                System.Windows.Controls.StackPanel fieldPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };

                System.Windows.Controls.TextBlock label = new System.Windows.Controls.TextBlock { Text = prop.Name + ":", Width = 100, VerticalAlignment = VerticalAlignment.Center };
                fieldPanel.Children.Add(label);

                System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox { Width = 300, Text = prop.GetValue(CurrentIDCardInfo)?.ToString() ?? string.Empty };
                textBox.Tag = prop.Name; // Store property name in Tag for later retrieval
                fieldPanel.Children.Add(textBox);

                // Add color coding based on FieldStatus
                PropertyInfo? statusProp = typeof(IDCardInfo).GetProperty(prop.Name + "Status");
                if (statusProp != null)
                {
                    FieldStatus status = (FieldStatus)(statusProp.GetValue(CurrentIDCardInfo) ?? FieldStatus.NotFound);
                    switch (status)
                    {
                        case FieldStatus.Failed:
                            textBox.Background = new SolidColorBrush(Colors.Red);
                            break;
                        case FieldStatus.Partial:
                            textBox.Background = new SolidColorBrush(Colors.Yellow);
                            break;
                        case FieldStatus.NotFound:
                            textBox.Background = new SolidColorBrush(Colors.LightGray);
                            break;
                        case FieldStatus.Success:
                            // No special background for success
                            break;
                    }
                }

                EditFieldsPanel.Children.Add(fieldPanel);
            }
        }

        private void CopyRawOcrText_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(RawOcrTextBox.Text);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Update CurrentIDCardInfo properties from textboxes
            foreach (System.Windows.Controls.StackPanel fieldPanel in EditFieldsPanel.Children)
            {
                if (fieldPanel.Children.Count > 1 && fieldPanel.Children[1] is System.Windows.Controls.TextBox textBox)
                {
                    string? propName = textBox.Tag as string;
                    if (propName != null)
                    {
                        PropertyInfo? prop = typeof(IDCardInfo).GetProperty(propName);
                        if (prop != null)
                        {
                            prop.SetValue(CurrentIDCardInfo, textBox.Text);
                            // Optionally, update status to Success if manually edited
                            PropertyInfo? statusProp = typeof(IDCardInfo).GetProperty(propName + "Status");
                            if (statusProp != null)
                            {
                                statusProp.SetValue(CurrentIDCardInfo, FieldStatus.Success);
                            }
                        }
                    }
                }
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
