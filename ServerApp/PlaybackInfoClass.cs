using System;
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
        static public byte[] thumbnail;

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
                thumbnail = null;
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
                    }
                    else
                        thumbnail = null;
                }
                else
                    thumbnail = null;
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
