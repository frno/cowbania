using System.Diagnostics;
using Cowbania.Host.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Cowbania.Host.Audio;

internal sealed class AudioEventBus
{
    private static readonly IReadOnlyDictionary<AudioEvent, string> Files = new Dictionary<AudioEvent, string>
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

    private readonly Dictionary<AudioEvent, IAudioPlayback> effects;
    private readonly HashSet<AudioEvent> disabledEvents = [];
    private readonly IAudioEffectLoader loader;
    private readonly Func<string, string> pathResolver;
    private bool initialized;

    public AudioEventBus() : this(null) { }

    internal AudioEventBus(
        IReadOnlyDictionary<AudioEvent, IAudioPlayback>? initialEffects,
        IAudioEffectLoader? effectLoader = null,
        Func<string, string>? pathResolver = null,
        bool initialized = false)
    {
        effects = initialEffects is null ? [] : new Dictionary<AudioEvent, IAudioPlayback>(initialEffects);
        loader = effectLoader ?? new ManagedWavEffectLoader();
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
        RuntimeLog.Info($"audio playback request event={audioEvent} loaded={effects.ContainsKey(audioEvent)} disabled={disabledEvents.Contains(audioEvent)}");
        if (disabledEvents.Contains(audioEvent))
        {
            RuntimeLog.Warn($"audio playback suppressed event={audioEvent} reason=disabled fallback=silence");
            return;
        }
        if (!effects.TryGetValue(audioEvent, out var effect))
        {
            effect = LoadEffect(audioEvent);
            if (effect is null)
            {
                Disable(audioEvent, "decode-or-unavailable");
                return;
            }
        }
        try
        {
            RuntimeLog.Info($"audio playback begin event={audioEvent} volume={Volumes[audioEvent]:F2}");
            var played = effect.Play(Volumes[audioEvent], 0, 0);
            RuntimeLog.Info($"audio playback result event={audioEvent} played={played}");
            if (!played) Disable(audioEvent, "playback-result");
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
        var fileName = Files[audioEvent];
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
            StartupDiagnostics.Mark($"audio load start: {fileName}");
            RuntimeLog.Info($"deferred managed audio decode begin event={audioEvent} file=\"{fileName}\" path=\"{path}\" bytes={new FileInfo(path).Length}");
            var effect = loader.Load(path);
            effects[audioEvent] = effect;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            StartupDiagnostics.Mark($"audio loaded: {fileName} ({elapsed:F1} ms)");
            RuntimeLog.Info($"deferred managed audio decode complete event={audioEvent} file=\"{fileName}\"");
            RuntimeLog.Info($"deferred audio load complete event={audioEvent} elapsedMs={elapsed:F1}");
            if (elapsed >= 100) RuntimeLog.Warn($"slow deferred audio load event={audioEvent} elapsedMs={elapsed:F1}");
            return effect;
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
        effects.Remove(audioEvent);
        RuntimeLog.Warn($"audio playback failure event={audioEvent} stage={stage} disabled=true fallback=silence");
    }

    private static string ResolvePath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Audio", fileName))
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
