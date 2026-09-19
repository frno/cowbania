namespace Cowbania.Core.Tests.Harness;

internal static class AnimationAssertions
{
    public static void AssertEnemyClips(
        IReadOnlyDictionary<PresentationAnimationState, AnimationClip> clips,
        string actorFolder,
        IEnumerable<(PresentationAnimationState State, string FrameName, int Count, float Fps, AnimationPlaybackMode Mode)> expected)
    {
        var definitions = expected.ToArray();
        Assert(clips.Count == definitions.Length, $"{actorFolder} has exactly its four stable clips");
        foreach (var (state, frameName, count, fps, mode) in definitions)
        {
            var clip = clips[state];
            Assert(clip.Frames.Length == count, $"{actorFolder} {frameName} has its authored frame count");
            Assert(MathF.Abs(clip.FramesPerSecond - fps) < 0.0001f,
                $"{actorFolder} {frameName} has its authored timing");
            Assert(clip.PlaybackMode == mode, $"{actorFolder} {frameName} has its authored playback mode");
            Assert(clip.Frames.Select(frame => frame.AssetKey).SequenceEqual(
                    Enumerable.Range(0, count)
                        .Select(index => $"Frontier/{actorFolder}/{frameName}_{index}.png")),
                $"{actorFolder} {frameName} maps every exact Frontier asset");
        }
    }

    public static bool IsSupported(RoomDefinition room, Vector2 position) =>
        room.Solids.Any(surface =>
            MathF.Abs(position.Y - surface.Y) < 0.01f &&
            position.X >= surface.X &&
            position.X <= surface.Right);
}
