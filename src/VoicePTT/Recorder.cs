using NAudio.Wave;

namespace VoicePTT;

/// <summary>Mikrofonaufnahme in 16 Bit Mono ueber WaveIn (MME), analog zum alten sounddevice-Client.</summary>
public sealed class Recorder : IDisposable
{
    private readonly object _gate = new();
    private WaveInEvent _wave;
    private MemoryStream _pcm;
    private TaskCompletionSource<bool> _stopped;

    public bool IsRecording { get; private set; }
    public int SampleRate { get; private set; } = 16000;
    public string ActiveDeviceName { get; private set; } = "";

    public static List<(int Index, string Name)> Devices()
    {
        var list = new List<(int, string)>();
        try
        {
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var name = WaveInEvent.GetCapabilities(i).ProductName;
                if (!string.IsNullOrWhiteSpace(name)) list.Add((i, name));
            }
        }
        catch (Exception ex) { Log.Write("Geraeteliste fehlgeschlagen:", ex.Message); }
        return list;
    }

    /// <summary>null/leer -> Systemstandard (-1), Zahl -> Index, Text -> erster passender Geraetename.</summary>
    public static int Resolve(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return -1;
        if (int.TryParse(value, out var idx))
            return idx >= 0 && idx < WaveInEvent.DeviceCount ? idx : -1;

        foreach (var (i, name) in Devices())
            if (name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                value.Contains(name, StringComparison.OrdinalIgnoreCase))
                return i;

        Log.Write("Mikrofon '" + value + "' nicht gefunden, nutze Standard.");
        return -1;
    }

    public void Start(AppConfig cfg)
    {
        lock (_gate)
        {
            if (IsRecording) return;
            SampleRate = cfg.SampleRate;
            var wanted = Resolve(cfg.InputDevice);

            Exception last = null;
            foreach (var dev in wanted >= 0 ? new[] { wanted, -1 } : new[] { -1 })
            {
                try
                {
                    _pcm = new MemoryStream(SampleRate * 2 * 8);
                    _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _wave = new WaveInEvent
                    {
                        DeviceNumber = dev,
                        WaveFormat = new WaveFormat(SampleRate, 16, 1),
                        BufferMilliseconds = 50,
                        NumberOfBuffers = 4,
                    };
                    _wave.DataAvailable += OnData;
                    _wave.RecordingStopped += OnStopped;
                    _wave.StartRecording();
                    IsRecording = true;
                    ActiveDeviceName = dev < 0 ? "Standard" : WaveInEvent.GetCapabilities(dev).ProductName;
                    Log.Write("Aufnahme gestartet, Geraet=" + ActiveDeviceName, SampleRate + " Hz");
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Log.Write("Aufnahme-Fehler (Geraet " + dev + "):", ex.Message);
                    CleanUp();
                }
            }
            throw new InvalidOperationException("Kein Mikrofon verfuegbar." +
                                                (last == null ? "" : " " + last.Message));
        }
    }

    private void OnData(object sender, WaveInEventArgs e)
    {
        var buf = _pcm;
        if (buf == null) return;
        lock (buf) { buf.Write(e.Buffer, 0, e.BytesRecorded); }
    }

    private void OnStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception != null) Log.Write("RecordingStopped:", e.Exception.Message);
        _stopped?.TrySetResult(true);
    }

    /// <summary>Beendet die Aufnahme und liefert eine fertige WAV-Datei (leer, wenn nichts aufgenommen wurde).</summary>
    public async Task<byte[]> StopAsync()
    {
        WaveInEvent wave;
        MemoryStream pcm;
        Task done;
        lock (_gate)
        {
            if (!IsRecording) return Array.Empty<byte>();
            IsRecording = false;
            wave = _wave;
            pcm = _pcm;
            done = _stopped?.Task ?? Task.CompletedTask;
        }

        try { wave?.StopRecording(); } catch (Exception ex) { Log.Write("StopRecording:", ex.Message); }
        await Task.WhenAny(done, Task.Delay(2000));

        byte[] raw;
        lock (pcm) { raw = pcm.ToArray(); }

        lock (_gate) { CleanUp(); }
        return raw.Length == 0 ? Array.Empty<byte>() : Wav.Build(raw, SampleRate);
    }

    private void CleanUp()
    {
        if (_wave != null)
        {
            _wave.DataAvailable -= OnData;
            _wave.RecordingStopped -= OnStopped;
            try { _wave.Dispose(); } catch { }
            _wave = null;
        }
        _pcm?.Dispose();
        _pcm = null;
        _stopped = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            IsRecording = false;
            CleanUp();
        }
    }
}

public static class Wav
{
    public static byte[] Build(byte[] pcm16Mono, int sampleRate)
    {
        using var ms = new MemoryStream(pcm16Mono.Length + 44);
        using var w = new BinaryWriter(ms);
        var byteRate = sampleRate * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + pcm16Mono.Length);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);      // PCM
        w.Write((short)1);      // Mono
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write((short)2);      // Block-Align
        w.Write((short)16);     // Bits
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(pcm16Mono.Length);
        w.Write(pcm16Mono);
        w.Flush();
        return ms.ToArray();
    }

    public static double Seconds(byte[] wav, int sampleRate) =>
        wav.Length <= 44 ? 0 : (wav.Length - 44) / 2.0 / sampleRate;
}
