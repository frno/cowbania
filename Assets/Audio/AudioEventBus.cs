using System;
using UnityEngine;

/// <summary>
/// Central audio surface for gameplay and UI. Add this component to one scene
/// object, assign PlaceholderAudioConfig, then call AudioEventBus.Raise(...)
/// from gameplay without depending on an AudioSource or clip asset.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class AudioEventBus : MonoBehaviour
{
    [SerializeField] private PlaceholderAudioConfig audioConfig;
    [SerializeField] private AudioSource audioSource;

    public static event Action<AudioEvent> EventRaised;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    public static void Raise(AudioEvent audioEvent)
    {
        EventRaised?.Invoke(audioEvent);

        AudioEventBus bus = FindObjectOfType<AudioEventBus>();
        if (bus != null)
        {
            bus.Play(audioEvent);
        }
    }

    public void Play(AudioEvent audioEvent)
    {
        if (audioConfig == null || audioSource == null)
        {
            return;
        }

        AudioClip clip = audioConfig.GetClip(audioEvent);
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, audioConfig.GetVolume(audioEvent));
        }
    }
}
