using Microsoft.Xna.Framework.Audio;

namespace Cowbania.Host.Audio;

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

internal sealed class SoundEffectPlayback(SoundEffect soundEffect) : IAudioPlayback
{
    public bool Play(float volume, float pitch, float pan) => soundEffect.Play(volume, pitch, pan);
}
