using Mono.Zeroconf;
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

namespace ServerApp
{
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
        Thread udpServer;

        bool tcpServerStopped = true;
        bool ucpServerStopped = true;
        bool stopTcpServer = false;
        bool stopUdpServer = false;
        private bool connectedClientsManagerStopped = true;
        private bool stopConnectedClientsManager = true;

        public short port = 22222; //Zakres short jest wymuszany przez Zeroconf
        public string password = "";
        public string language;
        bool LoggingEnabled = false;
        System.Drawing.Point point = new System.Drawing.Point(); //Point wykorzystywany do zadawania pozycji kursora
        List<TcpClient> connectedTcpClients = new List<TcpClient>();
        List<NetworkStream> connectedClients = new List<NetworkStream>();

        Int32 changingConnectedClients = 0;

        bool windowLogEnabled;

        public MainWindow()
        {
            InitializeComponent();

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            language = cultureInfo.TwoLetterISOLanguageName == "pl"?"pl":"en";
                
            string[] commandLineArgs = Environment.GetCommandLineArgs();

            foreach (var item in commandLineArgs)
            {
                switch (item)
                {
                    //case "HideCmdWindow":
                    //    var handle = GetConsoleWindow();
                    //    ShowWindow(handle, SW_HIDE); //Ukrycie okna konsolowego
                    //    break;
                    case "Log":
                        LoggingEnabled = true; //Włączenie logowania błędów do pliku
                        break;
                    default:
                        break;
                }
            }

            try
            {
                if (!File.Exists("config.ini")) //Odczyt lub utworzenie pliku konfiguracyjnego
                {
                    StreamWriter ConfigFile = File.CreateText("config.ini");
                    ConfigFile.WriteLine("PORT=" + port.ToString());
                    ConfigFile.WriteLine("PASSWORD=" + password.ToString());
                    ConfigFile.WriteLine("LANGUAGE=" + language.ToString());
                    ConfigFile.Close();
                }
                else
                {
                    StreamReader ConfigFile = File.OpenText("config.ini");
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
                            default:
                                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ConfigFileError);
                                break;
                        }
                    }
                    ConfigFile.Close();
                }
            }
            catch (Exception error)
            {
                UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());

                if (LoggingEnabled)
                {
                    StreamWriter LogFile = File.CreateText("log.txt");
                    LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                    LogFile.Close();
                }
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
            }
            else
            {
                stopTcpServer = true;

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                    serverStateButton.Content = Properties.Resources.StoppingServer;
                    serverStateButton.IsEnabled = false;
                }));
            }
        }

        private void ConnectedClientsManager()
        {
            stopConnectedClientsManager = false;
            connectedClientsManagerStopped = false;

            Byte[] buffer = new Byte[9999]; //bufor do odbioru bajtów danych

            while (!stopConnectedClientsManager)
            {
                if (connectedClients.Count > 0)
                {
                    while (1 == Interlocked.Exchange(ref changingConnectedClients, 1)) ;

                    for (int i = connectedClients.Count - 1; i >= 0; --i) //DO ZOPTYMALIZOWANIA
                    {
                        try
                        {
                            connectedClients[i].WriteByte((byte) 'T');
                        }
                        catch (Exception)
                        {
                            connectedClients[i].Dispose();
                            connectedClients.RemoveAt(i);
                            UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientDisconnected);
                            continue;
                        }
                        if (connectedClients[i].DataAvailable)
                        {
                            AesCryptoServiceProvider _aes;
                            _aes = new AesCryptoServiceProvider();
                            _aes.KeySize = 256;
                            _aes.BlockSize = 128;

                            String responseData = String.Empty; //string wykorzystywany do przechowywania odebranych tekstów

                            Int32 bytes = connectedClients[i].Read(buffer, 0, buffer.Length); //odczyt danych z bufora

                            Byte[] data = new Byte[bytes];
                            Array.Copy(buffer, data, bytes);
                            Byte[] dataDecoded;

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
                                    StreamWriter LogFile = File.CreateText("log.txt");
                                    LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                                    LogFile.Close();
                                }
                                continue;
                            }

                            if (dataDecoded.Length == 0)
                                continue;

                            Commands command = (Commands)BitConverter.ToInt32(dataDecoded, 0); //wyodrębnienie odebranej komendy
                            switch (command)
                            {
                                case Commands.SEND_TEXT: //odebranie tekstu
                                    responseData = System.Text.Encoding.UTF8.GetString(dataDecoded, 4, dataDecoded.Length - 4);
                                    if (responseData == "\n")
                                        SendKeys.SendWait("{ENTER}");
                                    else
                                        SendKeys.SendWait(responseData);
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + command.ToString() + " Wiadomość: " + responseData);
                                    break;
                                case Commands.SEND_BACKSPACE: //odebranie klawisza BACKSPACE
                                    SendKeys.SendWait("{BACKSPACE}");
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + command.ToString());
                                    break;
                                case Commands.SEND_LEFT_MOUSE: //odebranie lewego przycisku myszy
                                    mouse_event(
                                        (uint)(MouseEventFlags.MOVE |
                                            MouseEventFlags.LEFTDOWN | MouseEventFlags.LEFTUP),
                                        0, 0, 0, 0);
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + command.ToString());
                                    break;
                                case Commands.SEND_RIGHT_MOUSE: //odebranie prawego przycisku myszy
                                    mouse_event(
                                        (uint)(MouseEventFlags.MOVE |
                                            MouseEventFlags.RIGHTDOWN | MouseEventFlags.RIGHTUP),
                                        0, 0, 0, 0);
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + command.ToString());
                                    break;
                                case Commands.SEND_MOVE_MOUSE: //odebranie przesunięcia kursora TODO: Usunięcie "magic numbers"
                                    double moveX = BitConverter.ToDouble(dataDecoded, 4);
                                    double moveY = BitConverter.ToDouble(dataDecoded, 12);
                                    point.X = System.Windows.Forms.Cursor.Position.X + quadraticFunction(moveX);
                                    point.Y = System.Windows.Forms.Cursor.Position.Y + quadraticFunction(moveY);
                                    System.Windows.Forms.Cursor.Position = point;
                                    UpdateLog(DateTime.Now.ToString("HH:mm:ss") + " " + Properties.Resources.ClientCommand + " " + command.ToString() + " Przesunięcie: " + quadraticFunction(moveX) + " " + quadraticFunction(moveY));
                                    break;
                                case Commands.SEND_NEXT: //odebranie polecenia odtworzenia następnego utworu
                                    keybd_event((byte)KeyboardEventFlags.NEXT, 0, 0, 0);
                                    break;
                                case Commands.SEND_PREVIOUS: //odebranie polecenia odtworzenia poprzedniego utworu
                                    keybd_event((byte)KeyboardEventFlags.PREV, 0, 0, 0);
                                    break;
                                case Commands.SEND_STOP: //odebranie polecenia zatrzymania odtwarzania
                                    keybd_event((byte)KeyboardEventFlags.STOP, 0, 0, 0);
                                    break;
                                case Commands.SEND_PLAYSTOP: //odebranie polecenia wstrzymania/wznowienia odtwarzania
                                    keybd_event((byte)KeyboardEventFlags.PLAYPAUSE, 0, 0, 0);
                                    break;
                                case Commands.SEND_VOLDOWN: //odebranie polecenia podgłośnienia
                                    keybd_event((byte)KeyboardEventFlags.VOLDOWN, 0, 0, 0);
                                    break;
                                case Commands.SEND_VOLUP: //odebranie polecenia ściszenia
                                    keybd_event((byte)KeyboardEventFlags.VOLUP, 0, 0, 0);
                                    break;
                                case Commands.SEND_OPEN_WEBPAGE:  //odebranie polecenia otwarcia strony internetowej
                                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                                    startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                                    startInfo.FileName = "cmd.exe";
                                    startInfo.Arguments = "/C explorer \"http://" + Encoding.ASCII.GetString(dataDecoded.Skip(4).ToArray()).Trim('\0') + "\""; //parametr '/C' jest wymagany do prawidłowego działania polecenia
                                    process.StartInfo = startInfo;
                                    process.Start();
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    Interlocked.Exchange(ref changingConnectedClients, 0);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }

            foreach (var item in connectedClients)
            {
                item.Dispose();
            }
            connectedClients.Clear();

            connectedClientsManagerStopped = true;
        }

        private void TcpServer()
        {
            stopTcpServer = false;
            tcpServerStopped = false;

            while (!connectedClientsManagerStopped) ;
            Thread connectedClientsManagerThread = new Thread(new ThreadStart(ConnectedClientsManager));
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
                        StreamWriter LogFile = File.CreateText("log.txt");
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


                            while (1 == Interlocked.Exchange(ref changingConnectedClients, 1));

                            NetworkStream networkStream = tcpClient.GetStream();
                            networkStream.WriteTimeout = 1000;
                            connectedClients.Add(networkStream);

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
                            StreamWriter LogFile = File.CreateText("log.txt");
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
                    StreamWriter LogFile = File.CreateText("log.txt");
                    LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + error.ToString());
                    LogFile.Close();
                }
            }

            service.Dispose();
            server.Stop();
            stopConnectedClientsManager = true;

            tcpServerStopped = true;
            while (!connectedClientsManagerStopped) { }
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                serverStateButton.Content = Properties.Resources.StartServer;
                serverStateButton.IsEnabled = true;
            }));
        }

        private void UdpServer()
        {

        }

        private void UpdateLog(string newMessage, bool ignoreLogConfiguration = false)
        {
            if (windowLogEnabled || ignoreLogConfiguration)
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { logTextBox.Text += newMessage + "\n"; }));
        }

        private void logTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            logTextBox.CaretIndex = logTextBox.Text.Length;
            logTextBox.ScrollToEnd();
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
            SettingsWindow settingsWindow = new SettingsWindow(port, password, language);
            settingsWindow.port += value => port = value;
            settingsWindow.password += value => password = value;
            settingsWindow.language += value => language = value;
            settingsWindow.ShowDialog();
        }
    }
}
