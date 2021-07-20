﻿using Mono.Zeroconf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Globalization;
using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.CompilerServices;
using static ServerApp.PlaybackInfoClass;

namespace ServerApp
{
    public class TextBoxAttachedProperties // TextBox autoscroll
    {

        public static bool GetAutoScrollToEnd(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToEndProperty);
        }

        public static void SetAutoScrollToEnd(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToEndProperty, value);
        }

        // Using a DependencyProperty as the backing store for AutoScrollToEnd.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AutoScrollToEndProperty =
        DependencyProperty.RegisterAttached("AutoScrollToEnd", typeof(bool), typeof(TextBoxAttachedProperties), new PropertyMetadata(false, AutoScrollToEndPropertyChanged));

        private static void AutoScrollToEndPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.TextBox textbox && e.NewValue is bool mustAutoScroll && mustAutoScroll)
            {
                textbox.TextChanged += (s, ee) => AutoScrollToEnd(s, ee, textbox);
            }
        }

        private static void AutoScrollToEnd(object sender, TextChangedEventArgs e, System.Windows.Controls.TextBox textbox)
        {
            textbox.ScrollToEnd();
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [Flags]
        public enum MouseEventFlags : uint //flagi odpowiedzialne za akcje myszy
        {
            LEFTDOWN = 0x00000002,
            LEFTUP = 0x00000004,
            MIDDLEDOWN = 0x00000020,
            MIDDLEUP = 0x00000040,
            MOVE = 0x00000001,
            ABSOLUTE = 0x00008000,
            RIGHTDOWN = 0x00000008,
            RIGHTUP = 0x00000010,
            WHEEL = 0x00000800,
            XDOWN = 0x00000080,
            XUP = 0x00000100
        }
        public enum KeyboardEventFlags : uint //flagi odpowiedzialne za akcje klawiatury
        {
            PLAYPAUSE = 0xB3,
            NEXT = 0xB0,
            PREV = 0xB1,
            STOP = 0xB2,
            VOLUP = 0xAF,
            VOLDOWN = 0xAE
        }

        [DllImport("user32.dll")] //dołączenie biblioteki pozwalającej na ingerencję w akcje generowane przez mysz
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        [DllImport("user32.dll")] //dołączenie biblioteki pozwalającej na ingerencję w akcje generowane przez mysz
        private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] //dołączenie biblioteki pozwalającej na ingerencję w akcje generowane przez klawiaturę
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        static public int quadraticFunction(double x) //funkcja kwadratowa do lepszej jakości sterowania kursorem TODO: DODAĆ INERCJĘ I RZĘDU
        {
            const double coefficient1 = 0.8;
            const double coefficient2 = 1.0;
            return (x < 0.0 ? -1 : 1) * ((int)(((int)Math.Ceiling(x * x)) * coefficient1) + (int)(((int)Math.Ceiling((x < 0.0 ? -1.0 : 1.0) * x)) * coefficient2));
        }

        private static byte[] GenerateSalt(int size, string password)
        {
            var buffer = new byte[size];
            var passBytes = ASCIIEncoding.ASCII.GetBytes(password);

            if (passBytes.Length > buffer.Length) Array.Copy(passBytes, buffer, buffer.Length);
            else Array.Copy(passBytes, buffer, passBytes.Length);

            return buffer;
        }

        Thread tcpServer;

        bool tcpServerStopped = true;
        bool ucpServerStopped = true;
        bool stopTcpServer = false;
        bool stopUdpServer = false;
        private bool connectedClientsManagerStopped = true;
        private bool stopConnectedClientsManager = true;

        public short port = 22222; //Zakres short jest wymuszany przez Zeroconf
        public string password = "";
        public string language = "en";
        bool LoggingEnabled = false;
        System.Drawing.Point point = new System.Drawing.Point(); //Point wykorzystywany do zadawania pozycji kursora
        List<TcpClient> connectedTcpClients = new List<TcpClient>();

        class ConnectedClient
        {
            public NetworkStream networkStream { get; set; }
            public bool justConnected { get; set; }

            public ConnectedClient()
            {
                justConnected = true;
            }
        }
        List<ConnectedClient> connectedClients = new List<ConnectedClient>();

        Int32 changingConnectedClients = 0;

        public bool windowLogEnabled { set; get; }
        bool settingsChanged;
        private bool logging;
        private bool autostart;

        public MainWindow()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            language = cultureInfo.TwoLetterISOLanguageName == "pl" ? "pl" : "en";

            string[] commandLineArgs = Environment.GetCommandLineArgs();

            bool hideWindow = false;
            bool started = false;
            foreach (var item in commandLineArgs)
            {
                switch (item)
                {
                    case "Log":
                        LoggingEnabled = true; //Włączenie logowania błędów do pliku
                        break;
                    case "HideWindow":
                        hideWindow = true; //Ukrywa okno po uruchomieniu aplikacji
                        break;
                    case "Started":
                        started = true; //Uruchamia serwer od razu po uruchomieniu aplikacji
                        break;
                    default:
                        break;
                }
            }

            try
            {
                if (!File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\config.ini")) //Odczyt lub utworzenie pliku konfiguracyjnego
                {
                    UpdateConfigFile(port, password, language, "False", autostart.ToString());
                }
                else
                {
                    using (StreamReader ConfigFile = File.OpenText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\config.ini"))
                    {
                        string ConfigFileLine;
                        while ((ConfigFileLine = ConfigFile.ReadLine()) != null)
                        {
                            string[] value = ConfigFileLine.Split('=');
                            switch (value[0])
                            {
                                case "PORT":
                                    port = short.Parse(value[1]);
                                    break;
                                case "PASSWORD":
                                    password = value[1];
                                    break;
                                case "LANGUAGE":
                                    language = value[1];
                                    break;
                                case "LOGGING":
                                    logging = value[1] == "True" ? true : false;
                                    break;
                                case "AUTOSTART":
                                    autostart = value[1] == "True" ? true : false;
                                    break;
                                default:
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ConfigFileError);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());

                if (LoggingEnabled)
                {
                    StreamWriter LogFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\log.txt");
                    LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                    LogFile.Close();
                }
            }

            ChangeUILanguage(language);

            InitializeComponent();
            MyNotifyIcon.Visibility = Visibility.Collapsed;

            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server");
            UpdateLog(Properties.Resources.AppDataDirectory + " " + Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server", true);

            enableWindowLogCheckbox.IsChecked = logging;

            this.Closing += MainWindow_Closing;
            this.StateChanged += MainWindow_StateChanged;

            if (hideWindow)
            {
                WindowState = WindowState.Minimized;
                MyNotifyIcon.Visibility = Visibility.Visible;
                Hide();
            }

            if (started)
                serverStateButton_Click(null, null);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            switch (this.WindowState)
            {
                case WindowState.Maximized:
                    break;
                case WindowState.Minimized:
                    MyNotifyIcon.Visibility = Visibility.Visible;
                    Hide();
                    break;
                case WindowState.Normal:
                    break;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateConfigFile(port, password, language, windowLogEnabled.ToString(), autostart.ToString());
        }

        private void UpdateConfigFile(short port, string password, string language, string logging, string autostart)
        {
            using (StreamWriter ConfigFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\config.ini"))
            {
                ConfigFile.WriteLine("PORT=" + port.ToString());
                ConfigFile.WriteLine("PASSWORD=" + password);
                ConfigFile.WriteLine("LANGUAGE=" + language);
                ConfigFile.WriteLine("LOGGING=" + logging);
                ConfigFile.WriteLine("AUTOSTART=" + autostart);
            }
        }

        private void ChangeUILanguage(string language)
        {
            switch (language)
            {
                case "en":
                    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en");
                    break;
                default:
                    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("pl-PL");
                    break;
            }
        }

        private void serverStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (tcpServerStopped)
            {
                logTextBox.Text = "";
                tcpServerStopped = false;
                stopTcpServer = false;
                tcpServer = new Thread(new ThreadStart(TcpServer));
                tcpServer.Start();
                startServerTrayButton.IsEnabled = false;
                stopServerTrayButton.IsEnabled = true;
            }
            else
            {
                stopTcpServer = true;
                stopServerTrayButton.IsEnabled = false;

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    serverStateButton.Content = Properties.Resources.StoppingServer;
                    serverStateButton.IsEnabled = false;
                }));
            }
        }

        private void ConnectedClientsManager()
        {
            PlaybackInfoClass.Start();

            stopConnectedClientsManager = false;
            connectedClientsManagerStopped = false;

            byte[] buffer = new byte[9999]; //bufor do odbioru bajtów danych

            byte[] commandPing;
            byte[] dataToSendPing;
            byte[] dataToSendEncodedPing = new byte[0];
            commandPing = BitConverter.GetBytes((int)CommandsFromServer.SEND_PING);

            AesCryptoServiceProvider _aes;
            _aes = new AesCryptoServiceProvider();
            _aes.KeySize = 256;
            _aes.BlockSize = 128;
            _aes.Padding = PaddingMode.Zeros;

            dataToSendPing = new byte[commandPing.Length];
            byte[] dataToSendEncodedPingWithLength = new byte[0];
            Buffer.BlockCopy(commandPing, 0, dataToSendPing, 0, commandPing.Length);

            try
            {
                using (var pass = new PasswordDeriveBytes(password, GenerateSalt(_aes.BlockSize / 8, password)))
                {
                    using (var stream = new MemoryStream())
                    {
                        _aes.Key = pass.GetBytes(_aes.KeySize / 8);
                        _aes.IV = pass.GetBytes(_aes.BlockSize / 8);

                        var proc = _aes.CreateEncryptor();
                        using (var crypto = new CryptoStream(stream, proc, CryptoStreamMode.Write))
                        {
                            crypto.Write(dataToSendPing, 0, dataToSendPing.Length);
                            crypto.Clear();
                            crypto.Close();
                        }
                        stream.Close();

                        dataToSendEncodedPing = stream.ToArray();
                    }
                }

                dataToSendEncodedPingWithLength = new byte[sizeof(int) + dataToSendEncodedPing.Length];
                Buffer.BlockCopy(BitConverter.GetBytes(dataToSendEncodedPing.Length), 0, dataToSendEncodedPingWithLength, 0, sizeof(int));
                Buffer.BlockCopy(dataToSendEncodedPing, 0, dataToSendEncodedPingWithLength, sizeof(int), dataToSendEncodedPing.Length);
            }
            catch (Exception error)
            {
                UpdateLog(error.ToString());
            }

            DateTime pingLastTime = DateTime.Now;

            while (!stopConnectedClientsManager)
            {
                if (connectedClients.Count > 0)
                {
                    while (1 == Interlocked.Exchange(ref changingConnectedClients, 1)) ;

                    bool changedLock = false;

                    string playbackInfoString;

                    byte[] command = new byte[0];
                    byte[] data = new byte[0];
                    byte[] dataToSend = new byte[0];
                    byte[] dataToSendEncoded = new byte[0];
                    byte[] dataToSendEncodedWithLength = new byte[0];

                    if (PlaybackInfoClass.mediaPropertiesChanged)
                    {
                        while (PlaybackInfoClass.mediaPropertiesLock) ;
                        mediaPropertiesLock = true;
                        changedLock = true;

                        playbackInfoString = PlaybackInfoClass.playing.ToString() + "\u0006" + PlaybackInfoClass.artist + "\u0006" + PlaybackInfoClass.title + '\0';

                        command = BitConverter.GetBytes((int)CommandsFromServer.SEND_PLAYBACK_INFO);
                        int playbackInfoStringLength = System.Text.Encoding.UTF8.GetByteCount(playbackInfoString);
                        byte[] playbackInfoStringLengthByte = BitConverter.GetBytes(playbackInfoStringLength);
                        data = System.Text.Encoding.UTF8.GetBytes(playbackInfoString);
                        int playbackInfoThumbnailLength = 0;
                        if (PlaybackInfoClass.thumbnail != null)
                        {
                            playbackInfoThumbnailLength = thumbnail.Length;
                        }
                        byte[] playbackInfoThumbnailLengthByte = BitConverter.GetBytes(playbackInfoThumbnailLength);

                        dataToSend = new Byte[command.Length + playbackInfoStringLengthByte.Length + playbackInfoThumbnailLengthByte.Length + data.Length + playbackInfoThumbnailLength];
                        Buffer.BlockCopy(command, 0, dataToSend, 0, command.Length);
                        Buffer.BlockCopy(playbackInfoStringLengthByte, 0, dataToSend, command.Length, playbackInfoStringLengthByte.Length);
                        Buffer.BlockCopy(playbackInfoThumbnailLengthByte, 0, dataToSend, command.Length + playbackInfoStringLengthByte.Length, playbackInfoThumbnailLengthByte.Length);
                        Buffer.BlockCopy(data, 0, dataToSend, command.Length + playbackInfoStringLengthByte.Length + playbackInfoThumbnailLengthByte.Length, data.Length);
                        if (thumbnail != null)
                            Buffer.BlockCopy(thumbnail, 0, dataToSend, command.Length + playbackInfoStringLengthByte.Length + playbackInfoThumbnailLengthByte.Length + data.Length, thumbnail.Length);

                        using (var pass = new PasswordDeriveBytes(password, GenerateSalt(_aes.BlockSize / 8, password)))
                        {
                            using (var stream = new MemoryStream())
                            {
                                _aes.Key = pass.GetBytes(_aes.KeySize / 8);
                                _aes.IV = pass.GetBytes(_aes.BlockSize / 8);

                                var proc = _aes.CreateEncryptor();
                                using (var crypto = new CryptoStream(stream, proc, CryptoStreamMode.Write))
                                {
                                    crypto.Write(dataToSend, 0, dataToSend.Length);
                                    crypto.Clear();
                                    crypto.Close();
                                }
                                stream.Close();

                                dataToSendEncoded = stream.ToArray();
                            }
                        }

                        dataToSendEncodedWithLength = new byte[sizeof(int) + dataToSendEncoded.Length];
                        Buffer.BlockCopy(BitConverter.GetBytes(dataToSendEncoded.Length), 0, dataToSendEncodedWithLength, 0, sizeof(int));
                        Buffer.BlockCopy(dataToSendEncoded, 0, dataToSendEncodedWithLength, sizeof(int), dataToSendEncoded.Length);
                    }

                    bool timeToPingClients = (DateTime.Now - pingLastTime).Seconds > 5;
                    for (int i = connectedClients.Count - 1; i >= 0; --i) //DO ZOPTYMALIZOWANIA
                    {
                        // PING CLIENT EVERY ~5 SECONDS
                        if (timeToPingClients)
                        {
                            try
                            {
                                connectedClients[i].networkStream.Write(dataToSendEncodedPingWithLength, 0, dataToSendEncodedPingWithLength.Length);
                            }
                            catch (Exception)
                            {
                                connectedClients[i].networkStream.Dispose();
                                connectedClients.RemoveAt(i);
                                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientDisconnected);
                                continue;
                            }
                        }
                        /////////////////////////////////////

                        if (changedLock || connectedClients[i].justConnected)
                        {
                            try
                            {
                                connectedClients[i].networkStream.Write(dataToSendEncodedWithLength, 0, dataToSendEncodedWithLength.Length);
                                connectedClients[i].justConnected = false;
                            }
                            catch (Exception error)
                            {
                                UpdateLog(error.ToString());
                            }
                        }

                        if (connectedClients[i].networkStream.DataAvailable)
                        {
                            String responseData = String.Empty; //string wykorzystywany do przechowywania odebranych tekstów

                            Int32 bytes = connectedClients[i].networkStream.Read(buffer, 0, buffer.Length); //odczyt danych z bufora

                            for (int position = 0; position < bytes - sizeof(int);)
                            {
                                int messageLength = BitConverter.ToInt32(buffer, position);
                                position += sizeof(int);

                                if (position + messageLength > bytes)
                                    break; //odebrano niekompletną wiadomość

                                data = new Byte[messageLength];
                                Buffer.BlockCopy(buffer, position, data, 0, messageLength);
                                Byte[] dataDecoded = null;

                                position += messageLength;

                                try
                                {
                                    using (var pass = new PasswordDeriveBytes(password, GenerateSalt(_aes.BlockSize / 8, password)))
                                    {
                                        using (var MemoryStream = new MemoryStream())
                                        {
                                            _aes.Key = pass.GetBytes(_aes.KeySize / 8);
                                            _aes.IV = pass.GetBytes(_aes.BlockSize / 8);

                                            var proc = _aes.CreateDecryptor();
                                            using (var crypto = new CryptoStream(MemoryStream, proc, CryptoStreamMode.Write))
                                            {
                                                crypto.Write(data, 0, data.Length);
                                                crypto.Clear();
                                                crypto.Close();
                                            }
                                            MemoryStream.Close();

                                            dataDecoded = MemoryStream.ToArray();
                                        }
                                    }
                                }
                                catch (Exception error)
                                {
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.WrongClientPassword + " " + error.ToString());

                                    if (LoggingEnabled)
                                    {
                                        StreamWriter LogFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\log.txt");
                                        LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                                        LogFile.Close();
                                    }
                                    continue;
                                }

                                if (dataDecoded.Length <= sizeof(CommandsFromClient))
                                    continue;

                                CommandsFromClient commandFromClient = (CommandsFromClient)BitConverter.ToInt32(dataDecoded, 0); //wyodrębnienie odebranej komendy

                                try
                                {
                                    switch (commandFromClient)
                                    {
                                        case CommandsFromClient.SEND_TEXT: //odebranie tekstu
                                            responseData = System.Text.Encoding.UTF8.GetString(dataDecoded, 4, dataDecoded.Length - 4);
                                            if (responseData == "\n")
                                                SendKeys.SendWait("{ENTER}");
                                            else
                                                SendKeys.SendWait(responseData);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString() + " " + Properties.Resources.Message + " " + responseData);
                                            break;
                                        case CommandsFromClient.SEND_BACKSPACE: //odebranie klawisza BACKSPACE
                                            SendKeys.SendWait("{BACKSPACE}");
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_LEFT_MOUSE: //odebranie lewego przycisku myszy
                                            mouse_event(
                                                (uint)(MouseEventFlags.MOVE |
                                                    MouseEventFlags.LEFTDOWN | MouseEventFlags.LEFTUP),
                                                0, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_RIGHT_MOUSE: //odebranie prawego przycisku myszy
                                            mouse_event(
                                                (uint)(MouseEventFlags.MOVE |
                                                    MouseEventFlags.RIGHTDOWN | MouseEventFlags.RIGHTUP),
                                                0, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_LEFT_MOUSE_LONG_PRESS_START:
                                            mouse_event(
                                            (uint)(MouseEventFlags.LEFTDOWN),
                                            0, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_LEFT_MOUSE_LONG_PRESS_STOP:
                                            mouse_event(
                                            (uint)(MouseEventFlags.LEFTUP),
                                            0, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_MOVE_MOUSE: //odebranie przesunięcia kursora TODO: Usunięcie "magic numbers"
                                            double moveX = BitConverter.ToDouble(dataDecoded, 4);
                                            double moveY = BitConverter.ToDouble(dataDecoded, 12);
                                            point.X = System.Windows.Forms.Cursor.Position.X + quadraticFunction(moveX);
                                            point.Y = System.Windows.Forms.Cursor.Position.Y + quadraticFunction(moveY);
                                            System.Windows.Forms.Cursor.Position = point;
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString() + " " + Properties.Resources.Movement + " " + quadraticFunction(moveX) + " " + quadraticFunction(moveY));
                                            break;
                                        case CommandsFromClient.SEND_WHEEL_MOUSE: //odebranie polecenia obrócenia rolki myszy
                                            Int32 mouseWheelSliderValue = BitConverter.ToInt32(dataDecoded, 4);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString() + " mouseWheelSliderValue: " + mouseWheelSliderValue.ToString());
                                            if (mouseWheelSliderValue < -1 || mouseWheelSliderValue > 1)
                                            {
                                                const Int32 wheelCoef = 10;
                                                mouse_event((uint)MouseEventFlags.WHEEL, 0, 0, (uint)(wheelCoef * mouseWheelSliderValue), 0);
                                            }
                                            break;
                                        case CommandsFromClient.SEND_NEXT: //odebranie polecenia odtworzenia następnego utworu
                                            keybd_event((byte)KeyboardEventFlags.NEXT, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_PREVIOUS: //odebranie polecenia odtworzenia poprzedniego utworu
                                            keybd_event((byte)KeyboardEventFlags.PREV, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_STOP: //odebranie polecenia zatrzymania odtwarzania
                                            keybd_event((byte)KeyboardEventFlags.STOP, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_PLAYSTOP: //odebranie polecenia wstrzymania/wznowienia odtwarzania
                                            keybd_event((byte)KeyboardEventFlags.PLAYPAUSE, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_VOLDOWN: //odebranie polecenia podgłośnienia
                                            keybd_event((byte)KeyboardEventFlags.VOLDOWN, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_VOLUP: //odebranie polecenia ściszenia
                                            keybd_event((byte)KeyboardEventFlags.VOLUP, 0, 0, 0);
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        case CommandsFromClient.SEND_OPEN_WEBPAGE:  //odebranie polecenia otwarcia strony internetowej
                                            System.Diagnostics.Process process = new System.Diagnostics.Process();
                                            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                                            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                                            startInfo.FileName = "cmd.exe";
                                            startInfo.Arguments = "/C explorer \"http://" + Encoding.ASCII.GetString(dataDecoded.Skip(4).ToArray()).Trim('\0') + "\""; //parametr '/C' jest wymagany do prawidłowego działania polecenia
                                            process.StartInfo = startInfo;
                                            process.Start();
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + commandFromClient.ToString());
                                            break;
                                        default:
                                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.UnknownCommand);
                                            break;
                                    }
                                }
                                catch (Exception error)
                                {
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.WrongClientPassword + " " + error.ToString());

                                    if (LoggingEnabled)
                                    {
                                        StreamWriter LogFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\log.txt");
                                        LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                                        LogFile.Close();
                                    }
                                }
                            }
                        }
                    }

                    if (timeToPingClients)
                        pingLastTime = DateTime.Now;

                    if (changedLock)
                    {
                        PlaybackInfoClass.mediaPropertiesChanged = false;
                        mediaPropertiesLock = false;
                    }

                    Interlocked.Exchange(ref changingConnectedClients, 0);

                    Thread.Sleep(5);
                }
                else
                {
                    PlaybackInfoClass.mediaPropertiesChanged = true;
                    Thread.Sleep(500);
                }
            }

            foreach (var item in connectedClients)
            {
                item.networkStream.Dispose();
            }
            connectedClients.Clear();

            connectedClientsManagerStopped = true;

            PlaybackInfoClass.Stop();
        }

        private void TcpServer()
        {
            stopTcpServer = false;
            tcpServerStopped = false;

            while (!connectedClientsManagerStopped) ;
            Thread connectedClientsManagerThread = new Thread(new ThreadStart(ConnectedClientsManager));
            connectedClientsManagerThread.IsBackground = true;
            connectedClientsManagerThread.Start();

            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { serverStateButton.Content = Properties.Resources.StopServer; }));

            RegisterService service = new RegisterService(); //Utworzenie obiektu odpowiedzialnego za działanie grupy technik Zeroconf;

            IPAddress adres_ip = IPAddress.Any;
            TcpListener server = new TcpListener(adres_ip, port);

            try
            {
                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ServerStarted + "\n" + DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ConnectionParams + " " + adres_ip.ToString() + ":" + port.ToString(), true);
                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ConnectionPassword + " " + password, true);
                server.Start();

                //////////////////////Zeroconf////////////////////
                try
                {
                    service.Name = Environment.MachineName + " Pilot Server"; //Nazwa usługi
                    service.RegType = "_pilotServer._tcp"; //Typ usługi
                    service.ReplyDomain = "local."; //Domena
                    service.Port = port; //Port
                    service.Register(); //Uruchomienie Zeroconf z powyższą konfiguracją
                }
                catch (Exception error)
                {
                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());

                    if (LoggingEnabled)
                    {
                        StreamWriter LogFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\log.txt");
                        LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                        LogFile.Close();
                    }
                }
                //////////////////////////////////////////////////

                while (!stopTcpServer)
                {
                    try
                    {
                        if (server.Pending())
                        {
                            TcpClient tcpClient = server.AcceptTcpClient();
                            connectedTcpClients.Add(tcpClient);
                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientConnected + " " + tcpClient.Client.RemoteEndPoint.ToString(), true);


                            while (1 == Interlocked.Exchange(ref changingConnectedClients, 1)) ;

                            NetworkStream networkStream = tcpClient.GetStream();
                            networkStream.WriteTimeout = 1000;
                            ConnectedClient connectedClient = new ConnectedClient();
                            connectedClient.networkStream = networkStream;
                            connectedClients.Add(connectedClient);

                            Interlocked.Exchange(ref changingConnectedClients, 0);
                        }
                        else
                        {
                            for (int i = connectedTcpClients.Count - 1; i >= 0; --i)  //DO ZOPTYMALIZOWANIA
                            {
                                if (!connectedTcpClients[i].Connected)
                                {
                                    connectedTcpClients.RemoveAt(i);
                                }
                            }
                            Thread.Sleep(3000);
                        }
                    }
                    catch (Exception error)
                    {
                        UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());

                        if (LoggingEnabled)
                        {
                            StreamWriter LogFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\log.txt");
                            LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                            LogFile.Close();
                        }
                    }
                }
            }
            catch (Exception error)
            {
                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());

                if (LoggingEnabled)
                {
                    StreamWriter LogFile = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Pilot Server\\log.txt");
                    LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                    LogFile.Close();
                }
            }

            service.Dispose();
            server.Stop();
            stopConnectedClientsManager = true;

            tcpServerStopped = true;

            while (!connectedClientsManagerStopped) { }

            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (serverStateButton != null) serverStateButton.Content = Properties.Resources.StartServer;
                    if (serverStateButton != null) serverStateButton.IsEnabled = true;
                    if (startServerTrayButton != null) startServerTrayButton.IsEnabled = true;
                }));
        }

        private void UpdateLog(string newMessage, bool ignoreLogConfiguration = false)
        {
            if (windowLogEnabled || ignoreLogConfiguration)
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { 
                    logTextBox.Text += newMessage + "\n";
                    logTextBox.Focus();
                }));
        }

        private void logTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            logTextBox.CaretIndex = logTextBox.Text.Length;
            logTextBox.ScrollToEnd();
            logTextBox.Focus();
        }

        private void EnableWindowLogCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            windowLogEnabled = true;
        }

        private void EnableWindowLogCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            windowLogEnabled = false;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow(port, password, language, enableWindowLogCheckbox.IsChecked.Value.ToString(), autostart);
            settingsWindow.port += value => port = value;
            settingsWindow.password += value => password = value;
            settingsWindow.language += value => language = value;
            settingsWindow.autostart += value => autostart = value;

            settingsChanged = false;
            settingsWindow.settingsChanged += value => settingsChanged = value;

            settingsWindow.Closed += SettingsWindow_Closed;
            settingsWindow.ShowDialog();
        }

        private void SettingsWindow_Closed(object sender, EventArgs e)
        {
            if (settingsChanged)
                ChangeUILanguage(language);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //clean up notifyicon (would otherwise stay open until application finishes)
            MyNotifyIcon.Dispose();

            if (!tcpServerStopped)
                serverStateButton_Click(null, null);

            base.OnClosing(e);
        }

        private void StartServerTray_Click(object sender, RoutedEventArgs e)
        {
            serverStateButton_Click(null, null);
        }

        private void StopServerTray_Click(object sender, RoutedEventArgs e)
        {
            serverStateButton_Click(null, null);
        }

        private void ShowWindowTray_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            MyNotifyIcon.Visibility = Visibility.Collapsed;
        }

        private void ExitAppTray_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowWindowTray_Click(null, null);
        }
    }
}
