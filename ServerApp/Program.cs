using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ServerApp
{
    class Program
    {
        // See http://www.pinvoke.net/default.aspx/user32/mouse_event.html
        // Mouse event flags.
        [Flags]
        public enum MouseEventFlags : uint
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

        // Use the values of this enum for the 'dwData' parameter
        // to specify an X button when using MouseEventFlags.XDOWN or
        // MouseEventFlags.XUP for the dwFlags parameter.
        public enum MouseEventDataXButtons : uint
        {
            XBUTTON1 = 0x00000001,
            XBUTTON2 = 0x00000002
        }

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        static public int quadraticFunction(double x) //DODAĆ INERCJĘ I RZĘDU
        {
            const double coefficient1 = 0.8;
            const double coefficient2 = 1.0;
            return (x < 0.0 ? -1 : 1) * ((int)(((int)Math.Ceiling(x * x)) * coefficient1) + (int)(((int)Math.Ceiling((x < 0.0 ? -1.0 : 1.0) * x)) * coefficient2));
        }

        static void Main(string[] args)
        {
            TcpListener serwer;
            TcpClient klient;
            try
            {
                String strHostName = string.Empty;
                strHostName = Dns.GetHostName();
                IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
                IPAddress[] addr = ipEntry.AddressList;
                //IPAddress adres_ip = IPAddress.Parse(addr[3].ToString());
                //IPAddress adres_ip = IPAddress.Parse("127.0.0.1");
                IPAddress adres_ip = IPAddress.Any;
                System.Console.WriteLine("{0} Serwer uruchomiony.\n{0} Dane do polaczenia: {1}:1234", DateTime.Now.ToString("HH:mm:ss"), adres_ip.ToString());
                serwer = new TcpListener(adres_ip, 1234);
                serwer.Start();
                while (true)
                {
                    try
                    {
                        klient = serwer.AcceptTcpClient();
                        System.Console.WriteLine("{0} Polaczono z klientem.", DateTime.Now.ToString("HH:mm:ss"));
                        NetworkStream stream = klient.GetStream();
                        // Buffer to store the response bytes.
                        Byte[] data = new Byte[9999];

                        // String to store the response ASCII representation.
                        String responseData = String.Empty;

                        // Read the first batch of the TcpServer response bytes.
                        Int32 bytes = stream.Read(data, 0, data.Length);
                        Commands command = (Commands) BitConverter.ToInt32(data, 0);
                        switch (command)
                        {
                            case Commands.SEND_TEXT:
                                responseData = System.Text.Encoding.UTF8.GetString(data, 4, bytes - 4);
                                if (responseData == "\n")
                                    SendKeys.SendWait("{ENTER}");
                                else
                                    SendKeys.SendWait(responseData);
                                Console.WriteLine("{0} Komenda: {1} Wiadomość: \"{2}\"", DateTime.Now.ToString("HH:mm:ss"), command.ToString(), responseData);
                                break;
                            case Commands.SEND_BACKSPACE:
                                SendKeys.SendWait("{BACKSPACE}");
                                Console.WriteLine("{0} Komenda: {1}", DateTime.Now.ToString("HH:mm:ss"), command.ToString());
                                break;
                            case Commands.SEND_LEFT_MOUSE:
                                mouse_event(
                                    (uint)(MouseEventFlags.MOVE |
                                        MouseEventFlags.LEFTDOWN | MouseEventFlags.LEFTUP),
                                    0, 0, 0, 0);
                                Console.WriteLine("{0} Komenda: {1}", DateTime.Now.ToString("HH:mm:ss"), command.ToString());
                                break;
                            case Commands.SEND_RIGHT_MOUSE:
                                mouse_event(
                                    (uint)(MouseEventFlags.MOVE |
                                        MouseEventFlags.RIGHTDOWN | MouseEventFlags.RIGHTUP),
                                    0, 0, 0, 0);
                                Console.WriteLine("{0} Komenda: {1}", DateTime.Now.ToString("HH:mm:ss"), command.ToString());
                                break;
                            case Commands.SEND_MOVE_MOUSE:
                                double moveX = BitConverter.ToDouble(data, 4);
                                double moveY = BitConverter.ToDouble(data, 12);
                                Cursor.Position = new Point(Cursor.Position.X + quadraticFunction(moveX), Cursor.Position.Y + quadraticFunction(moveY));
                                Console.WriteLine("{0} Komenda: {1} Przesunięcie: {2} {3}", DateTime.Now.ToString("HH:mm:ss"), command.ToString(), quadraticFunction(moveX), quadraticFunction(moveY));
                                break;
                            default:
                                break;
                        }

                        //// Close everything.
                        stream.Close();
                        klient.Close();
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
