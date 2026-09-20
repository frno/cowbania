using Cowbania.Host.Diagnostics;

namespace Cowbania.Host.Audio;

/// <summary>
/// Plays a single looping background music track. Distinct from
/// <see cref="AudioEventBus"/>, which fires short one-shot gameplay SFX --
/// this owns exactly one long-lived looping instance and starts it once at
/// startup rather than lazily per gameplay event.
/// </summary>
internal sealed class MusicPlayer
{
    private const string FileName = "Music_Background.wav";
    private const float Volume = 0.45f;

    private readonly IMusicLoader loader;
    private readonly Func<string, string> pathResolver;
    private readonly IAudioInitialization? audioInitialization;
    private IMusicPlayback? playback;
    private bool disabled;
    private bool startRequested;

    public MusicPlayer() : this(new ManagedWavMusicLoader(), ResolvePath) { }

    public MusicPlayer(IAudioInitialization audioInitialization) : this(
        new ManagedWavMusicLoader(),
        ResolvePath,
        audioInitialization) { }

    internal MusicPlayer(
        IMusicLoader loader,
        Func<string, string> pathResolver,
        IAudioInitialization? audioInitialization = null)
    {
        this.loader = loader;
        this.pathResolver = pathResolver;
        this.audioInitialization = audioInitialization;
    }

    public void Start()
    {
        startRequested = true;
        if (audioInitialization is not null)
        {
            RuntimeLog.Info("background music requested; waiting for audio device initialization");
            return;
        }
        TryStart();
    }

    public void Update()
    {
        if (!startRequested || disabled || playback is not null || audioInitialization is null)
            return;
        if (audioInitialization.State is AudioInitializationState.NotStarted or AudioInitializationState.Pending)
            return;
        if (audioInitialization.State == AudioInitializationState.Failed)
        {
            RuntimeLog.Warn("background music disabled because audio device initialization failed");
            disabled = true;
            return;
        }
        TryStart();
    }

    private void TryStart()
    {
        if (disabled || playback is not null) return;

        var path = pathResolver(FileName);
        if (!File.Exists(path))
        {
            RuntimeLog.Warn($"background music missing path=\"{path}\"");
            disabled = true;
            return;
        }

        try
        {
            RuntimeLog.Info($"background music load begin path=\"{path}\"");
            playback = loader.Load(path);
            playback.Play(Volume);
            RuntimeLog.Info("background music playback started");
        }
        catch (Exception exception)
        {
            RuntimeLog.Error("background music load/playback failure", exception);
            disabled = true;
            playback = null;
        }
    }

    public void Stop() => playback?.Stop();

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
