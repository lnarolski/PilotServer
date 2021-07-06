using System;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace ServerApp
{
    class PlaybackInfoClass
    {
        public static async Task Test()
        {
            var gsmtcsm = await GetSystemMediaTransportControlsSessionManager();
            var mediaProperties = await GetMediaProperties(gsmtcsm.GetCurrentSession());

            Console.WriteLine("{0} - {1}", mediaProperties.Artist, mediaProperties.Title);
        }

        private static async Task<GlobalSystemMediaTransportControlsSessionManager> GetSystemMediaTransportControlsSessionManager() =>
            await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        private static async Task<GlobalSystemMediaTransportControlsSessionMediaProperties> GetMediaProperties(GlobalSystemMediaTransportControlsSession session) =>
            await session.TryGetMediaPropertiesAsync();
    }
}
