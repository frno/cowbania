using System.IO;
using System.Diagnostics;
using System.Buffers.Binary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

internal enum AudioEvent
{
    Shooting,
    Reload,
    Jump,
    Dash,
    Pickup,
    Damage
}

internal sealed class AudioEventBus
{
    private static readonly IReadOnlyDictionary<AudioEvent, string> AudioFiles = new Dictionary<AudioEvent, string>
    {
        [AudioEvent.Shooting] = "SFX_Shooting.wav",
        [AudioEvent.Reload] = "SFX_Reload.wav",
        [AudioEvent.Jump] = "SFX_Jump.wav",
        [AudioEvent.Dash] = "SFX_Dash.wav",
        [AudioEvent.Pickup] = "SFX_Pickup.wav",
        [AudioEvent.Damage] = "SFX_Damage.wav"
    };

    private static readonly IReadOnlyDictionary<AudioEvent, float> Volumes = new Dictionary<AudioEvent, float>
    {
        [AudioEvent.Shooting] = 0.75f,
        [AudioEvent.Reload] = 0.7f,
        [AudioEvent.Jump] = 0.8f,
        [AudioEvent.Dash] = 0.85f,
        [AudioEvent.Pickup] = 0.8f,
        [AudioEvent.Damage] = 0.8f
    };

    private readonly Dictionary<AudioEvent, IAudioPlayback> soundEffects;
    private readonly HashSet<AudioEvent> disabledEvents = [];
    private readonly IAudioEffectLoader effectLoader;
    private readonly Func<string, string> pathResolver;
    private bool initialized;

    public AudioEventBus()
        : this(null)
    {
    }

    internal AudioEventBus(
        IReadOnlyDictionary<AudioEvent, IAudioPlayback>? initialSoundEffects,
        IAudioEffectLoader? effectLoader = null,
        Func<string, string>? pathResolver = null,
        bool initialized = false)
    {
        soundEffects = initialSoundEffects is null
            ? new Dictionary<AudioEvent, IAudioPlayback>()
            : new Dictionary<AudioEvent, IAudioPlayback>(initialSoundEffects);
        this.effectLoader = effectLoader ?? new ManagedWavEffectLoader();
        this.pathResolver = pathResolver ?? ResolvePath;
        this.initialized = initialized;
    }

    public void Load(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
        initialized = true;
        StartupDiagnostics.Mark("audio loading deferred until first sound event");
        RuntimeLog.Info("audio subsystem ready; managed WAV decoding remains deferred");
    }

    public void Play(AudioEvent audioEvent)
    {
        RuntimeLog.Info(
            $"audio playback request event={audioEvent} loaded={soundEffects.ContainsKey(audioEvent)} disabled={disabledEvents.Contains(audioEvent)}");
        if (disabledEvents.Contains(audioEvent))
        {
            RuntimeLog.Warn($"audio playback suppressed event={audioEvent} reason=disabled fallback=silence");
            return;
        }

        if (!soundEffects.TryGetValue(audioEvent, out var soundEffect))
        {
            soundEffect = LoadEffect(audioEvent);
            if (soundEffect is null)
            {
                Disable(audioEvent, "decode-or-unavailable");
                return;
            }
        }

        try
        {
            RuntimeLog.Info($"audio playback begin event={audioEvent} volume={Volumes[audioEvent]:F2}");
            var played = soundEffect.Play(Volumes[audioEvent], 0f, 0f);
            RuntimeLog.Info($"audio playback result event={audioEvent} played={played}");
            if (!played)
                Disable(audioEvent, "playback-result");
        }
        catch (Exception exception)
        {
            RuntimeLog.Error($"audio playback failure event={audioEvent}", exception);
            Disable(audioEvent, "playback");
        }
    }

    private IAudioPlayback? LoadEffect(AudioEvent audioEvent)
    {
        if (!initialized)
        {
            RuntimeLog.Warn($"audio event {audioEvent} ignored before subsystem initialization");
            return null;
        }

        var fileName = AudioFiles[audioEvent];
        var path = pathResolver(fileName);
        if (!File.Exists(path))
        {
            StartupDiagnostics.Mark($"audio missing: {fileName}");
            RuntimeLog.Warn($"deferred audio load missing event={audioEvent} path=\"{path}\"");
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var fileLength = new FileInfo(path).Length;
            StartupDiagnostics.Mark($"audio load start: {fileName}");
            RuntimeLog.Info(
                $"deferred managed audio decode begin event={audioEvent} file=\"{fileName}\" path=\"{path}\" bytes={fileLength}");
            var soundEffect = effectLoader.Load(path);
            RuntimeLog.Info($"deferred managed audio decode complete event={audioEvent} file=\"{fileName}\"");
            soundEffects[audioEvent] = soundEffect;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            StartupDiagnostics.Mark($"audio loaded: {fileName} ({elapsed:F1} ms)");
            RuntimeLog.Info($"deferred audio load complete event={audioEvent} elapsedMs={elapsed:F1}");
            if (elapsed >= 100)
                RuntimeLog.Warn($"slow deferred audio load event={audioEvent} elapsedMs={elapsed:F1}");
            return soundEffect;
        }
        catch (Exception exception)
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            StartupDiagnostics.Mark($"audio load failed: {fileName} ({elapsed:F1} ms): {exception.GetType().Name}: {exception.Message}");
            RuntimeLog.Error($"audio decode failure event={audioEvent} elapsedMs={elapsed:F1} path=\"{path}\"", exception);
            return null;
        }
    }

    private void Disable(AudioEvent audioEvent, string stage)
    {
        disabledEvents.Add(audioEvent);
        soundEffects.Remove(audioEvent);
        RuntimeLog.Warn(
            $"audio playback failure event={audioEvent} stage={stage} disabled=true fallback=silence");
    }

    private static string ResolvePath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Audio", fileName))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return candidates[0];
    }
}

internal interface IAudioPlayback
{
    bool Play(float volume, float pitch, float pan);
}

internal interface IAudioEffectLoader
{
    IAudioPlayback Load(string path);
}

internal sealed class ManagedWavEffectLoader : IAudioEffectLoader
{
    public IAudioPlayback Load(string path)
    {
        var wav = ManagedPcmWav.Read(File.ReadAllBytes(path));
        return new SoundEffectPlayback(new SoundEffect(wav.PcmData, wav.SampleRate, wav.Channels));
    }
}

internal readonly record struct ManagedPcmWav(byte[] PcmData, int SampleRate, AudioChannels Channels)
{
    private const uint Riff = 0x46464952;
    private const uint Wave = 0x45564157;
    private const uint Format = 0x20746D66;
    private const uint Data = 0x61746164;

    public static ManagedPcmWav Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Riff ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != Wave)
        {
            throw new InvalidDataException("The audio file is not a RIFF/WAVE file.");
        }

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (riffSize > bytes.Length - 8)
            throw new InvalidDataException("The RIFF container length exceeds the file length.");

        ushort? channelCount = null;
        int? sampleRate = null;
        ushort? blockAlign = null;
        byte[]? pcmData = null;

        var offset = 12;
        while (offset <= bytes.Length - 8)
        {
            var chunkId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            var chunkStart = offset + 8;
            if (chunkLength > bytes.Length - chunkStart)
                throw new InvalidDataException("A WAV chunk length exceeds the file length.");

            var chunk = bytes.Slice(chunkStart, checked((int)chunkLength));
            if (chunkId == Format)
            {
                if (chunk.Length < 16)
                    throw new InvalidDataException("The WAV format chunk is incomplete.");

                var encoding = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                var parsedChannelCount = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                var rate = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
                var alignment = BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]);
                var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);

                if (encoding != 1 || bitsPerSample != 16)
                    throw new InvalidDataException("Only uncompressed 16-bit PCM WAV audio is supported.");
                if (parsedChannelCount is not (1 or 2))
                    throw new InvalidDataException("Only mono or stereo WAV audio is supported.");
                if (rate is 0 or > int.MaxValue)
                    throw new InvalidDataException("The WAV sample rate is invalid.");
                if (alignment != parsedChannelCount * 2)
                    throw new InvalidDataException("The WAV block alignment is invalid.");

                channelCount = parsedChannelCount;
                sampleRate = (int)rate;
                blockAlign = alignment;
            }
            else if (chunkId == Data)
            {
                pcmData = chunk.ToArray();
            }

            var paddedLength = (long)chunkLength + (chunkLength & 1);
            var nextOffset = (long)chunkStart + paddedLength;
            if (nextOffset > bytes.Length)
                throw new InvalidDataException("The padded WAV chunk length exceeds the file length.");
            offset = (int)nextOffset;
        }

        if (channelCount is null || sampleRate is null || blockAlign is null)
            throw new InvalidDataException("The WAV format chunk is missing.");
        if (pcmData is null || pcmData.Length == 0)
            throw new InvalidDataException("The WAV data chunk is missing or empty.");
        if (pcmData.Length % blockAlign.Value != 0)
            throw new InvalidDataException("The WAV sample data is not block aligned.");

        var channels = channelCount == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        return new ManagedPcmWav(pcmData, sampleRate.Value, channels);
    }
}

internal sealed class SoundEffectPlayback(SoundEffect soundEffect) : IAudioPlayback
{
    public bool Play(float volume, float pitch, float pan) => soundEffect.Play(volume, pitch, pan);
}
