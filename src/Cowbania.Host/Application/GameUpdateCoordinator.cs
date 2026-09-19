using Cowbania.Host.Audio;
using Cowbania.Host.Diagnostics;
using Cowbania.Host.Feedback;
using Cowbania.Host.Input;
using Cowbania.Host.Presentation;

namespace Cowbania.Host.Application;

internal sealed class GameUpdateCoordinator(
    GameWorld world,
    KeyboardInputMapper inputMapper,
    PresentationTimeline timeline,
    AudioFeedbackRouter audioFeedback,
    GameplayTransitionLogger transitionLogger)
{
    private long frame;

    public void Update(float elapsedSeconds)
    {
        frame++;
        var input = inputMapper.Read();
        var signals = Capture(input);

        if (input.JumpPressed)
            transitionLogger.LogJumpRequest(frame, world);

        world.Update(input, elapsedSeconds);

        if (input.JumpPressed)
            transitionLogger.LogJumpOutcome(frame, world);

        transitionLogger.Log(frame, signals, world);
        if (signals.PlayerDiedOrChangedRoom(world))
            timeline.Reset(world);

        if (signals.SimulationActive(world))
        {
            audioFeedback.Route(signals, world, frame);
            timeline.Advance(elapsedSeconds, signals, world);
        }
    }

    private FrameFeedbackSnapshot Capture(InputFrame input) => new(
        input,
        world.Health,
        world.CollectedPickupCount,
        world.IsReloading,
        world.IsPaused,
        world.Room,
        world.ObjectivePhase,
        world.CheckpointRoom,
        world.CheckpointPosition,
        world.Enemies.ToArray(),
        world.Projectiles.ToArray());
}
