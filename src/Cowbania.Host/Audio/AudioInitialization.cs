using System.Diagnostics;
using Cowbania.Host.Diagnostics;
using Microsoft.Xna.Framework.Audio;

namespace Cowbania.Host.Audio;

internal enum AudioInitializationState
{
    NotStarted,
    Pending,
    Ready,
    Failed
}

internal interface IAudioInitialization
{
    AudioInitializationState State { get; }
    Exception? Failure { get; }
    void Start();
}

/// <summary>
/// Initializes MonoGame's native audio backend once, away from the game thread.
/// OpenAL can spend tens of seconds probing an unavailable device, so callers poll
/// <see cref="State"/> instead of waiting for initialization to finish.
/// </summary>
internal sealed class AudioInitialization : IAudioInitialization
{
    private readonly Action initialize;
    private int state = (int)AudioInitializationState.NotStarted;
    private Exception? failure;

    public AudioInitialization() : this(SoundEffect.Initialize) { }

    internal AudioInitialization(Action initialize) => this.initialize = initialize;

    public AudioInitializationState State => (AudioInitializationState)Volatile.Read(ref state);
    public Exception? Failure => Volatile.Read(ref failure);

    public void Start()
    {
        if (Interlocked.CompareExchange(
                ref state,
                (int)AudioInitializationState.Pending,
                (int)AudioInitializationState.NotStarted) != (int)AudioInitializationState.NotStarted)
            return;

        StartupDiagnostics.Mark("audio device initialization scheduled in background");
        _ = Task.Run(Initialize);
    }

    private void Initialize()
    {
        var started = Stopwatch.GetTimestamp();
        RuntimeLog.Info("audio device initialization begin");
        try
        {
            initialize();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Volatile.Write(ref state, (int)AudioInitializationState.Ready);
            RuntimeLog.Info($"audio device initialization complete elapsedMs={elapsed:F1}");
        }
        catch (Exception exception)
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Volatile.Write(ref failure, exception);
            Volatile.Write(ref state, (int)AudioInitializationState.Failed);
            RuntimeLog.Error($"audio device initialization failed elapsedMs={elapsed:F1}; audio disabled", exception);
        }
    }
}