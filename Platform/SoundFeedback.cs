using System;
using System.IO;
using System.Media;
using System.Text;

namespace Prunel
{
    /// <summary>Which sound confirms an activation (see SoundFeedback).</summary>
    internal enum SoundKind
    {
        /// <summary>No sound.</summary>
        None,

        /// <summary>A key of the gaze keyboard: short and high "tick" ([KeySound] setting).</summary>
        Key,

        /// <summary>Any other button (menu, action choice, settings...): softer two-note "pop" ([ButtonSound] setting).</summary>
        Button
    }

    /// <summary>
    /// PLATFORM: the optional sound feedback. A short sound confirms that a button or a key
    /// was just activated by the gaze. Two separate settings, both "no" by default:
    /// [KeySound] for the keys of the gaze keyboard, [ButtonSound] for all the other buttons.
    ///
    /// The two sounds are not files: they are computed once, at the first use, as a small
    /// WAV in memory (a few milliseconds of a fading sine wave). They are played with
    /// System.Media.SoundPlayer, provided with .NET Framework 4.8: no extra library.
    /// Play() returns at once (the sound plays in the background), so the gaze is never slowed
    /// down. A new sound cuts the previous one: with fast typing, the ticks follow each other.
    ///
    /// The sound goes to the default Windows output, at the Windows volume.
    /// </summary>
    internal static class SoundFeedback
    {
        // 22050 samples per second, 16 bits, mono: enough for such short sounds.
        private const int SampleRate = 22050;

        // Volume of the sounds, from 0 to 1: rather low, so as not to tire with fast typing.
        private const double Volume = 0.24;

        // Created at the first use (null before, or after a failure).
        private static SoundPlayer _keyPlayer;
        private static SoundPlayer _buttonPlayer;

        // True after a failure (no sound card...): we no longer try, and log it only once.
        private static bool _failed;

        /// <summary>
        /// Plays the sound of this kind, if its setting is on. Does nothing for SoundKind.None.
        /// Never throws: a sound problem must not stop Prunel.
        /// </summary>
        public static void Play(SoundKind kind)
        {
            AppSettings p = AppSettings.Current;
            bool wanted = (kind == SoundKind.Key && p.KeySound) || (kind == SoundKind.Button && p.ButtonSound);
            if (!wanted || _failed)
            {
                return;
            }

            try
            {
                if (_keyPlayer == null)
                {
                    // Key: a single high note (2000 Hz), 25 ms.
                    _keyPlayer = MakePlayer(Wave(new[] { 2000.0 }, 0.025));
                    // Button: two notes going up (880 then 1320 Hz), 40 ms each.
                    _buttonPlayer = MakePlayer(Wave(new[] { 880.0, 1320.0 }, 0.040));
                }
                (kind == SoundKind.Key ? _keyPlayer : _buttonPlayer).Play();
            }
            catch (Exception e)
            {
                _failed = true;
                App.Log("Sound feedback: cannot play sounds (" + e.Message + ")");
            }
        }

        /// <summary>A SoundPlayer ready to play this WAV (read once, here).</summary>
        private static SoundPlayer MakePlayer(byte[] wav)
        {
            SoundPlayer player = new SoundPlayer(new MemoryStream(wav));
            player.Load();
            return player;
        }

        /// <summary>
        /// Builds a WAV file in memory: the notes one after the other, each one lasting
        /// [noteSeconds], fading quickly (like a small bell). A very short rise at the start of
        /// each note avoids a "crack" in the speaker.
        /// </summary>
        private static byte[] Wave(double[] frequencies, double noteSeconds)
        {
            int perNote = (int)(SampleRate * noteSeconds);
            int count = perNote * frequencies.Length;
            int rise = SampleRate / 1000 * 2;   // 2 ms

            short[] samples = new short[count];
            for (int n = 0; n < frequencies.Length; n++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    double t = (double)i / SampleRate;
                    double fade = Math.Exp(-5.0 * i / perNote);
                    double start = i < rise ? (double)i / rise : 1.0;
                    double value = Math.Sin(2 * Math.PI * frequencies[n] * t) * fade * start * Volume;
                    samples[n * perNote + i] = (short)(value * short.MaxValue);
                }
            }

            // The WAV format: a 44-byte header ("RIFF", "fmt ", "data"), then the samples.
            MemoryStream stream = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(stream, Encoding.ASCII))
            {
                int dataBytes = count * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                 // size of the "fmt " part
                w.Write((short)1);           // PCM (uncompressed)
                w.Write((short)1);           // mono
                w.Write(SampleRate);
                w.Write(SampleRate * 2);     // bytes per second
                w.Write((short)2);           // bytes per sample
                w.Write((short)16);          // bits per sample
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (short s in samples)
                {
                    w.Write(s);
                }
            }
            return stream.ToArray();
        }
    }
}
