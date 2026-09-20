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
    private IMusicPlayback? playback;
    private bool disabled;

    public MusicPlayer() : this(new ManagedWavMusicLoader(), ResolvePath) { }

    internal MusicPlayer(IMusicLoader loader, Func<string, string> pathResolver)
    {
        this.loader = loader;
        this.pathResolver = pathResolver;
    }

    public void Start()
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
