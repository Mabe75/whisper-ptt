using NAudio.Wave;

namespace VoicePTT;

/// <summary>Kurze Signaltoene mit einstellbarer Lautstaerke (winsound.Beep kannte keine).</summary>
public static class Tone
{
    private const int Rate = 44100;

    public static void Play(double freqHz, int ms, double volume)
    {
        if (volume <= 0 || ms <= 0) return;
        volume = Math.Min(volume, 1.0) * 0.3;

        try
        {
            var n = Rate * ms / 1000;
            var samples = new float[n];
            var fade = Math.Min(300, n / 2);
            for (var i = 0; i < n; i++)
            {
                var v = (float)(Math.Sin(2 * Math.PI * freqHz * i / Rate) * volume);
                if (fade > 0)
                {
                    if (i < fade) v *= (float)i / fade;
                    else if (i >= n - fade) v *= (float)(n - 1 - i) / fade;
                }
                samples[i] = v;
            }

            var bytes = new byte[n * 4];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

            var stream = new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1));
            var output = new WaveOutEvent { DesiredLatency = 120 };
            output.PlaybackStopped += (_, _) =>
            {
                try { output.Dispose(); } catch { }
                try { stream.Dispose(); } catch { }
            };
            output.Init(stream);
            output.Play();
        }
        catch (Exception ex)
        {
            Log.Write("Ton fehlgeschlagen:", ex.Message);
        }
    }
}
