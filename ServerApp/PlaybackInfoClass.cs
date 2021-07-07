using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace ServerApp
{
    struct PlaybackInfoStruct
    {
        public bool playing;
        public string artist;
        public string title;
    }

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
            }
            else
            {
                var mediaProperties = await globalSystemMediaTransportControlsSession.TryGetMediaPropertiesAsync();

                playing = true;
                artist = mediaProperties.Artist;
                title = mediaProperties.Title;
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
