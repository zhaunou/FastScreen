using System;
using System.IO;
using System.Media;
using System.Reflection;

namespace LocalScreenshot
{
    sealed class ScreenshotSound : IDisposable
    {
        internal const string ResourceName = "FastScreen.Screenshot.wav";
        Stream stream;
        SoundPlayer player;
        public bool Available { get { return player != null; } }

        public ScreenshotSound()
        {
            try
            {
                stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
                if (stream == null) return;
                player = new SoundPlayer(stream);
                player.Load();
            }
            catch { Dispose(); } // Audio failure must not prevent taking screenshots.
        }

        public void Play()
        {
            if (player == null) return;
            try { player.Play(); } // Async playback does not block the next screenshot.
            catch { } // A saved PNG remains successful if audio is unavailable.
        }

        public void Dispose()
        {
            if (player != null) { player.Dispose(); player = null; }
            if (stream != null) { stream.Dispose(); stream = null; }
        }
    }
}
