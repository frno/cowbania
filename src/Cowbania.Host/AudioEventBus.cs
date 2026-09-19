using System.IO;
using System.Diagnostics;
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
    private GraphicsDevice? graphicsDevice;

    public AudioEventBus()
        : this(null)
    {
    }

    internal AudioEventBus(IReadOnlyDictionary<AudioEvent, IAudioPlayback>? initialSoundEffects)
    {
        soundEffects = initialSoundEffects is null
            ? new Dictionary<AudioEvent, IAudioPlayback>()
            : new Dictionary<AudioEvent, IAudioPlayback>(initialSoundEffects);
    }

    public void Load(GraphicsDevice graphicsDevice)
    {
        this.graphicsDevice = graphicsDevice;
        StartupDiagnostics.Mark("audio loading deferred until first sound event");
        RuntimeLog.Info("audio subsystem ready; WAV decoding remains deferred");
    }

    public void Play(AudioEvent audioEvent)
    {
        RuntimeLog.Info($"audio playback request event={audioEvent} loaded={soundEffects.ContainsKey(audioEvent)}");
        if (!soundEffects.TryGetValue(audioEvent, out var soundEffect))
        {
            soundEffect = LoadEffect(audioEvent);
            if (soundEffect is null)
            {
                RuntimeLog.Warn($"audio playback failure event={audioEvent} stage=decode-or-unavailable");
                return;
            }
        }

        try
        {
            RuntimeLog.Info($"audio playback begin event={audioEvent} volume={Volumes[audioEvent]:F2}");
            var played = soundEffect.Play(Volumes[audioEvent], 0f, 0f);
            RuntimeLog.Info($"audio playback result event={audioEvent} played={played}");
        }
        catch (Exception exception)
        {
            RuntimeLog.Error($"audio playback failure event={audioEvent}", exception);
        }
    }

    private IAudioPlayback? LoadEffect(AudioEvent audioEvent)
    {
        if (graphicsDevice is null)
        {
            RuntimeLog.Warn($"audio event {audioEvent} ignored before subsystem initialization");
            return null;
        }

        var fileName = AudioFiles[audioEvent];
        var path = ResolvePath(fileName);
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
                $"deferred audio decode begin event={audioEvent} file=\"{fileName}\" path=\"{path}\" bytes={fileLength}");
            var soundEffect = new SoundEffectPlayback(SoundEffect.FromFile(path));
            RuntimeLog.Info($"deferred audio decode complete event={audioEvent} file=\"{fileName}\"");
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

internal sealed class SoundEffectPlayback(SoundEffect soundEffect) : IAudioPlayback
{
    public bool Play(float volume, float pitch, float pan) => soundEffect.Play(volume, pitch, pan);
}
