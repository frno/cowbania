using Microsoft.Xna.Framework.Audio;

namespace Cowbania.Host.Audio;

internal interface IMusicPlayback
{
    void Play(float volume);
    void Stop();
}

internal interface IMusicLoader
{
    IMusicPlayback Load(string path);
}

/// <summary>
/// Loads a managed PCM WAV and plays it via a looping <see cref="SoundEffectInstance"/>.
/// The WAV itself must already be prepared for seamless looping (see
/// tools/nanogpt/loop_music.py) -- MonoGame's IsLooped simply seeks back to
/// sample 0 the instant playback reaches the end, so any discontinuity
/// between the last and first sample would otherwise produce an audible
/// click on every loop.
/// </summary>
internal sealed class ManagedWavMusicLoader : IMusicLoader
{
    public IMusicPlayback Load(string path)
    {
        var wav = ManagedPcmWav.Read(File.ReadAllBytes(path));
        var effect = new SoundEffect(wav.PcmData, wav.SampleRate, wav.Channels);
        return new LoopingMusicPlayback(effect.CreateInstance());
    }
}

internal sealed class LoopingMusicPlayback(SoundEffectInstance instance) : IMusicPlayback
{
    public void Play(float volume)
    {
        instance.IsLooped = true;
        instance.Volume = Math.Clamp(volume, 0f, 1f);
        instance.Play();
    }

    public void Stop() => instance.Stop();
}
