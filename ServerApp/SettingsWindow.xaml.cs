using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ServerApp
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public Action<short> port;

        public SettingsWindow(short port, string password, string language)
        {
            InitializeComponent();

            connectionPortTextBox.Text = port.ToString();
            connectionPasswordTextBox.Text = password;
            if (language == "pl")
                appLangComboBox.SelectedIndex = 0;
            else
                appLangComboBox.SelectedIndex = 1;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
