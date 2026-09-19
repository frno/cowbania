using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cowbania/Audio/Placeholder Audio Config")]
public sealed class PlaceholderAudioConfig : ScriptableObject
{
    [SerializeField] private List<AudioEventDefinition> events = new List<AudioEventDefinition>();

    public AudioClip GetClip(AudioEvent audioEvent)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].eventType == audioEvent)
            {
                return events[i].clip;
            }
        }

        return null;
    }

    public float GetVolume(AudioEvent audioEvent)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].eventType == audioEvent)
            {
                return Mathf.Max(0f, events[i].volume);
            }
        }

        return 1f;
    }
}

[Serializable]
public struct AudioEventDefinition
{
    public AudioEvent eventType;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume;
}
