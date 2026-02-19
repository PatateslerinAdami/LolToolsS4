using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LolEditor
{
    public partial class AddPropertyWindow : Window
    {
        public bool UseRawHash { get; private set; }
        public uint FinalHash { get; private set; }

        public string SectionName { get; private set; }
        public string PropName { get; private set; }

        public string PropValue { get; private set; }
        public int SelectedTypeId { get; private set; }

        public AddPropertyWindow()
        {
            InitializeComponent();
        }

        private void ChkRawHash_Changed(object sender, RoutedEventArgs e)
        {
            bool isRaw = ChkRawHash.IsChecked == true;

            TxtSection.IsEnabled = !isRaw;
            TxtName.IsEnabled = !isRaw;

            TxtHash.IsEnabled = isRaw;
            TxtHash.Background = isRaw ? Brushes.White : Brushes.LightGray;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            UseRawHash = ChkRawHash.IsChecked == true;
            SectionName = TxtSection.Text;
            PropName = TxtName.Text;
            PropValue = TxtValue.Text;

            if (CmbType.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag.ToString(), out int id))
            {
                SelectedTypeId = id;
            }

            if (UseRawHash)
            {
                string hashText = TxtHash.Text.Trim();
                if (hashText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    hashText = hashText.Substring(2);
                    if (!uint.TryParse(hashText, NumberStyles.HexNumber, null, out uint h))
                    {
                        MessageBox.Show("Invalid Hex Hash format.");
                        return;
                    }
                    FinalHash = h;
                }
                else
                {
                    if (!uint.TryParse(hashText, out uint h))
                    {
                        MessageBox.Show("Invalid Decimal Hash format.");
                        return;
                    }
                    FinalHash = h;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(SectionName) || string.IsNullOrWhiteSpace(PropName))
                {
                    MessageBox.Show("Please enter both Section Name and Property Name.");
                    return;
                }
            }

            DialogResult = true;
        }
    }
}