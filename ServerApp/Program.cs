﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using Mono.Zeroconf;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace ServerApp
{
    class Program
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
        [DllImport("user32.dll")] //dołączenie biblioteki pozwalającej na ingerencję w akcje generowane przez klawiaturę
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        
        //////////////////////////////BIBLIOTEKI DO UKRYWANIA OKNA KONSOLOWEGO/////////////////////////////////////
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

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

        static void Main(string[] args)
        {
            TcpListener server;
            TcpClient client;
            short port = 22222; //Zakres short jest wymuszany przez Zeroconf
            string password = "";

            foreach (var item in args)
            {
                switch (item)
                {
                    case "HideCmdWindow":
                        var handle = GetConsoleWindow();
                        ShowWindow(handle, SW_HIDE); //Ukrycie okna konsolowego
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
                            default:
                                System.Console.WriteLine("{0} NIEPRAWIDLOWY ODCZYT Z PLIKU config.ini", DateTime.Now.ToString("HH:mm:ss"));
                                break;
                        }
                    }
                    ConfigFile.Close();
                }
            }
            catch (Exception error)
            {
                Console.Write("{0} ", DateTime.Now.ToString("HH:mm:ss"));
                System.Console.WriteLine(error.ToString());
            }

            try
            {
                IPAddress adres_ip = IPAddress.Any;
                System.Console.WriteLine("{0} Serwer uruchomiony.\n{0} Dane do polaczenia: {1}:{2}", DateTime.Now.ToString("HH:mm:ss"), adres_ip.ToString(), port.ToString());
                server = new TcpListener(adres_ip, port);

                AesCryptoServiceProvider _aes;
                _aes = new AesCryptoServiceProvider();
                _aes.KeySize = 256;
                _aes.BlockSize = 128;

                //////////////////////Zeroconf////////////////////
                try
                {
                    RegisterService service = new RegisterService(); //Utworzenie obiektu odpowiedzialnego za działanie grupy technik Zeroconf
                    service.Name = "Pilot Server"; //Nazwa usługi
                    service.RegType = "_pilotServer._tcp"; //Typ usługi
                    service.ReplyDomain = "local."; //Domena
                    service.Port = port; //Port
                    service.Register(); //Uruchomienie Zeroconf z powyższą konfiguracją
                }
                catch (Exception e)
                {
                    Console.Write("{0} ", DateTime.Now.ToString("HH:mm:ss"));
                    Console.WriteLine(e.ToString());
                }
                //////////////////////////////////////////////////

                server.Start();
                while (true)
                {
                    try
                    {
                        client = server.AcceptTcpClient();
                        System.Console.WriteLine("{0} Polaczono z klientem.", DateTime.Now.ToString("HH:mm:ss"));
                        NetworkStream stream = client.GetStream();
                        Byte[] buffer = new Byte[9999]; //bufor do odbioru bajtów danych
                        
                        String responseData = String.Empty; //string wykorzystywany do przechowywania odebranych tekstów
                        
                        Int32 bytes = stream.Read(buffer, 0, buffer.Length); //odczyt danych z bufora

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
                            Console.Write("{0} NIEPRAWIDLOWE HASLO W KONFIGURACJI KLIENTA: ", DateTime.Now.ToString("HH:mm:ss"));
                            System.Console.WriteLine(error.ToString());
                            continue;
                        }

                        if (dataDecoded.Length == 0)
                            continue;

                        Commands command = (Commands) BitConverter.ToInt32(dataDecoded, 0); //wyodrębnienie odebranej komendy
                        switch (command)
                        {
                            case Commands.SEND_TEXT: //odebranie tekstu
                                responseData = System.Text.Encoding.UTF8.GetString(dataDecoded, 4, dataDecoded.Length - 4);
                                if (responseData == "\n")
                                    SendKeys.SendWait("{ENTER}");
                                else
                                    SendKeys.SendWait(responseData);
                                Console.WriteLine("{0} Komenda: {1} Wiadomość: \"{2}\"", DateTime.Now.ToString("HH:mm:ss"), command.ToString(), responseData);
                                break;
                            case Commands.SEND_BACKSPACE: //odebranie klawisza BACKSPACE
                                SendKeys.SendWait("{BACKSPACE}");
                                Console.WriteLine("{0} Komenda: {1}", DateTime.Now.ToString("HH:mm:ss"), command.ToString());
                                break;
                            case Commands.SEND_LEFT_MOUSE: //odebranie lewego przycisku myszy
                                mouse_event(
                                    (uint)(MouseEventFlags.MOVE |
                                        MouseEventFlags.LEFTDOWN | MouseEventFlags.LEFTUP),
                                    0, 0, 0, 0);
                                Console.WriteLine("{0} Komenda: {1}", DateTime.Now.ToString("HH:mm:ss"), command.ToString());
                                break;
                            case Commands.SEND_RIGHT_MOUSE: //odebranie prawego przycisku myszy
                                mouse_event(
                                    (uint)(MouseEventFlags.MOVE |
                                        MouseEventFlags.RIGHTDOWN | MouseEventFlags.RIGHTUP),
                                    0, 0, 0, 0);
                                Console.WriteLine("{0} Komenda: {1}", DateTime.Now.ToString("HH:mm:ss"), command.ToString());
                                break;
                            case Commands.SEND_MOVE_MOUSE: //odebranie przesunięcia kursora TODO: Usunięcie "magic numbers"
                                double moveX = BitConverter.ToDouble(dataDecoded, 4);
                                double moveY = BitConverter.ToDouble(dataDecoded, 12);
                                Cursor.Position = new Point(Cursor.Position.X + quadraticFunction(moveX), Cursor.Position.Y + quadraticFunction(moveY));
                                Console.WriteLine("{0} Komenda: {1} Przesunięcie: {2} {3}", DateTime.Now.ToString("HH:mm:ss"), command.ToString(), quadraticFunction(moveX), quadraticFunction(moveY));
                                break;
                            case Commands.SEND_NEXT: //odebranie polecenia odtworzenia następnego utworu
                                keybd_event((byte) KeyboardEventFlags.NEXT, 0, 0, 0);
                                break;
                            case Commands.SEND_PREVIOUS: //odebranie polecenia odtworzenia poprzedniego utworu
                                keybd_event((byte) KeyboardEventFlags.PREV, 0, 0, 0);
                                break;
                            case Commands.SEND_STOP: //odebranie polecenia zatrzymania odtwarzania
                                keybd_event((byte) KeyboardEventFlags.STOP, 0, 0, 0);
                                break;
                            case Commands.SEND_PLAYSTOP: //odebranie polecenia wstrzymania/wznowienia odtwarzania
                                keybd_event((byte) KeyboardEventFlags.PLAYPAUSE, 0, 0, 0);
                                break;
                            case Commands.SEND_VOLDOWN: //odebranie polecenia podgłośnienia
                                keybd_event((byte) KeyboardEventFlags.VOLDOWN, 0, 0, 0);
                                break;
                            case Commands.SEND_VOLUP: //odebranie polecenia ściszenia
                                keybd_event((byte) KeyboardEventFlags.VOLUP, 0, 0, 0);
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

                        stream.Close();
                        client.Close();
                    }
                    catch (Exception error)
                    {
                        Console.Write("{0} ", DateTime.Now.ToString("HH:mm:ss"));
                        System.Console.WriteLine(error.ToString());
                    }
                }
            }
            catch (Exception error)
            {
                Console.Write("{0} ", DateTime.Now.ToString("HH:mm:ss"));
                System.Console.WriteLine(error.ToString());
            }
        }
    }
}
