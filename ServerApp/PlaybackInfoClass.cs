﻿using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ServerApp
{
    class PlaybackInfoClass
    {
        static bool started = false;

        public static bool mediaPropertiesChanged = false;
        public static bool mediaPropertiesLock = false;

        static GlobalSystemMediaTransportControlsSessionManager globalSystemMediaTransportControlsSessionManager;
        static GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession;

        static public bool playing = false;
        static public string artist = "";
        static public string title = "";
        static public System.Drawing.Bitmap thumbnailBitmap;
        public static byte[] thumbnail;

        public static async void Start()
        {
            globalSystemMediaTransportControlsSessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            globalSystemMediaTransportControlsSession = globalSystemMediaTransportControlsSessionManager.GetCurrentSession();

            globalSystemMediaTransportControlsSessionManager.CurrentSessionChanged += GlobalSystemMediaTransportControlsSessionManager_CurrentSessionChanged;

            if (globalSystemMediaTransportControlsSession != null)
            {
                GlobalSystemMediaTransportControlsSession_MediaPropertiesChanged(null, null);

                globalSystemMediaTransportControlsSession.MediaPropertiesChanged += GlobalSystemMediaTransportControlsSession_MediaPropertiesChanged;
            }

            started = true;
        }

        private static void GlobalSystemMediaTransportControlsSessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            if (globalSystemMediaTransportControlsSession != null)
            {
                globalSystemMediaTransportControlsSession.MediaPropertiesChanged -= GlobalSystemMediaTransportControlsSession_MediaPropertiesChanged;
            }

            globalSystemMediaTransportControlsSession = globalSystemMediaTransportControlsSessionManager.GetCurrentSession();

            if (globalSystemMediaTransportControlsSession != null)
            {
                globalSystemMediaTransportControlsSession.MediaPropertiesChanged += GlobalSystemMediaTransportControlsSession_MediaPropertiesChanged;
            }
        }

        private static async void GlobalSystemMediaTransportControlsSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            while (mediaPropertiesLock)
            {
                Thread.Sleep(100);
            }

            mediaPropertiesLock = true;

            if (globalSystemMediaTransportControlsSession == null)
            {
                playing = false;
                artist = ""; 
                title = "";
                thumbnailBitmap = null;
            }
            else
            {
                var mediaProperties = await globalSystemMediaTransportControlsSession.TryGetMediaPropertiesAsync();

                playing = true;
                artist = mediaProperties.Artist;
                title = mediaProperties.Title;
                var thumbnailOpenRead = mediaProperties.Thumbnail;
                if (thumbnailOpenRead != null)
                {
                    var thumbnailOpenReadStream = await thumbnailOpenRead.OpenReadAsync();
                    if (thumbnailOpenReadStream.CanRead)
                    {
                        thumbnail = new byte[thumbnailOpenReadStream.Size];
                        await thumbnailOpenReadStream.ReadAsync(thumbnail.AsBuffer(), (uint)thumbnailOpenReadStream.Size, InputStreamOptions.None);
                        using (MemoryStream ms = new MemoryStream(thumbnail))
                        {
                            ms.Position = 0;
                            thumbnailBitmap = new System.Drawing.Bitmap(ms);
                            using (MemoryStream stream = new MemoryStream())
                            {
                                thumbnailBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg); // Konwersja ze względu na błąd po stronie Androida w przypadku przesyłania pliku *.png: [skia] ------ png error IDAT: CRC error [skia] ---codec->getAndroidPixels() failed.
                                thumbnail = stream.ToArray();
                            }
                        }
                    }
                    else
                        thumbnailBitmap = null;
                }
                else
                    thumbnailBitmap = null;
            }

            mediaPropertiesChanged = true;
            mediaPropertiesLock = false;
        }

        public static void Stop()
        {
            if (globalSystemMediaTransportControlsSession != null)
            {
                globalSystemMediaTransportControlsSession.MediaPropertiesChanged -= GlobalSystemMediaTransportControlsSession_MediaPropertiesChanged;
            }

            globalSystemMediaTransportControlsSessionManager.CurrentSessionChanged -= GlobalSystemMediaTransportControlsSessionManager_CurrentSessionChanged;

            globalSystemMediaTransportControlsSessionManager = null;

            started = false;
        }
    }
}
