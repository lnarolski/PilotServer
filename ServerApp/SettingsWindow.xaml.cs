using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.IO;
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
        public Action<string> password;
        public Action<string> language;
        public Action<bool> settingsChanged;
        public Action<bool> autostart;

        string prevLang;
        bool prevAutostart;
        string logging;

        public SettingsWindow(short port, string password, string language, string logging, bool autostart)
        {
            InitializeComponent();

            this.logging = logging;

            prevLang = language;
            prevAutostart = autostart;

            connectionPortTextBox.Text = port.ToString();
            connectionPasswordTextBox.Text = password;
            if (language == "pl")
                appLangComboBox.SelectedIndex = 0;
            else
                appLangComboBox.SelectedIndex = 1;

            autostartCheckBox.IsChecked = autostart;

            connectionPortTextBox.ToolTip = new ToolTip()
            {
                Content = Properties.Resources.PortTextBoxToolTip
            };
            connectionPasswordTextBox.ToolTip = new ToolTip()
            {
                Content = Properties.Resources.PasswordTextBoxToolTip
            };
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            port(short.Parse(connectionPortTextBox.Text));
            password(connectionPasswordTextBox.Text);

            string lang = "";
            switch (appLangComboBox.Text)
            {
                case "Polski":
                    language("pl");
                    lang = "pl";
                    break;
                case "English":
                    language("en");
                    lang = "en";
                    break;
                default:
                    break;
            }

            using (StreamWriter ConfigFile = System.IO.File.CreateText(AppDomain.CurrentDomain.BaseDirectory + "\\config.ini"))
            {
                ConfigFile.WriteLine("PORT=" + short.Parse(connectionPortTextBox.Text));
                ConfigFile.WriteLine("PASSWORD=" + connectionPasswordTextBox.Text);
                ConfigFile.WriteLine("LANGUAGE=" + lang);
                ConfigFile.WriteLine("LOGGING=" + logging);
                ConfigFile.WriteLine("AUTOSTART=" + autostartCheckBox.IsChecked.ToString());
            }

            if (lang != prevLang)
                MessageBox.Show(Properties.Resources.RestartAppLang, Properties.Resources.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(Properties.Resources.RestartServer, Properties.Resources.Information, MessageBoxButton.OK, MessageBoxImage.Information);

            if (autostartCheckBox.IsChecked != prevAutostart)
            {
                string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutDir = startupDir + "\\" + "ServerApp" + ".lnk";

                if (System.IO.File.Exists(shortcutDir))
                    System.IO.File.Delete(shortcutDir);

                if ((bool) autostartCheckBox.IsChecked)
                {
                    object shStartup = (object)"Startup";
                    WshShell shell = new WshShell();
                    string shortcutAddress = shortcutDir;
                    IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
                    shortcut.Arguments = "HideWindow Started";
                    shortcut.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    shortcut.Save();
                }
            }

            settingsChanged(true);
            autostart((bool) autostartCheckBox.IsChecked);

            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ConnectionPortTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int temp = int.Parse(connectionPortTextBox.Text);

                if (temp > short.MaxValue)
                {
                    connectionPortTextBox.Text = short.MaxValue.ToString();
                }
                else if (temp < 0)
                {
                    connectionPortTextBox.Text = "0";
                }
            }
            catch (Exception)
            {
                connectionPortTextBox.Text = "";
            }
        }
    }
}
