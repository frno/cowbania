namespace Cowbania.Host.Tests.Harness;

internal static class AudioTestDoubles
{
    public static byte[] CreatePcmWav(byte[] pcmData, int sampleRate, short channels)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
        return stream.ToArray();
    }
}

internal sealed class ThrowingPlayback : IAudioPlayback
{
    public bool Play(float volume, float pitch, float pan) =>
        throw new InvalidOperationException("test playback failure");
}

internal sealed class RecordingPlayback(bool result = true) : IAudioPlayback
{
    public int PlayCount { get; private set; }
    public bool Play(float volume, float pitch, float pan)
    {
        PlayCount++;
        return result;
    }
}

internal sealed class RecordingLoader(IAudioPlayback playback) : IAudioEffectLoader
{
    public int LoadCount { get; private set; }
    public IAudioPlayback Load(string path)
    {
        LoadCount++;
        return playback;
    }
}

internal sealed class ThrowingLoader : IAudioEffectLoader
{
    public int LoadCount { get; private set; }
    public IAudioPlayback Load(string path)
    {
        LoadCount++;
        throw new InvalidDataException("test managed decode failure");
    }
}

internal sealed class RecordingMusicPlayback : IMusicPlayback
{
    public int PlayCount { get; private set; }
    public int StopCount { get; private set; }
    public float LastVolume { get; private set; }

    public void Play(float volume)
    {
        PlayCount++;
        LastVolume = volume;
    }

    public void Stop() => StopCount++;
}

internal sealed class RecordingMusicLoader(IMusicPlayback playback) : IMusicLoader
{
    public int LoadCount { get; private set; }
    public IMusicPlayback Load(string path)
    {
        LoadCount++;
        return playback;
    }
}

internal sealed class ThrowingMusicLoader : IMusicLoader
{
    public int LoadCount { get; private set; }
    public IMusicPlayback Load(string path)
    {
        LoadCount++;
        throw new InvalidDataException("test managed music decode failure");
    }
}

internal sealed class StubAudioInitialization(AudioInitializationState state) : IAudioInitialization
{
    public AudioInitializationState State { get; set; } = state;
    public Exception? Failure { get; set; }
    public int StartCount { get; private set; }
    public void Start() => StartCount++;
}
