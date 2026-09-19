using System.Numerics;
using Cowbania.Host.Feedback;

namespace Cowbania.Host.Presentation;

internal sealed class PresentationTimeline
{
    private readonly AnimationClock playerClock = new();
    private readonly Dictionary<string, PresentationAnimationClock> enemyClocks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BoundedEffectClock> defeatClocks = new(StringComparer.Ordinal);
    private readonly Dictionary<PickupType, PresentationAnimationClock> pickupClocks = new();

    public PresentationAnimationState PlayerState { get; private set; } = PresentationAnimationState.Idle;
    public float ShootSeconds { get; private set; }
    public float HurtSeconds { get; private set; }
    public float PickupSeconds { get; private set; }
    public Vector2 PickupPosition { get; private set; }
    public float ElapsedSeconds { get; private set; }

    public void Initialize(GameWorld world) => PrepareClocks(world);

    public void Reset(GameWorld world)
    {
        enemyClocks.Clear();
        defeatClocks.Clear();
        pickupClocks.Clear();
        ShootSeconds = 0;
        HurtSeconds = 0;
        PickupSeconds = 0;
        PrepareClocks(world);
    }

    public void Advance(float elapsedSeconds, FrameFeedbackSnapshot signals, GameWorld world)
    {
        var acceptedShot = world.PlayerShotAcceptedThisUpdate;
        if (acceptedShot) ShootSeconds = 0.14f;
        if (world.Health < signals.PreviousHealth) HurtSeconds = 0.35f;
        if (world.CollectedPickupCount > signals.PreviousPickupCount)
        {
            PickupSeconds = 0.32f;
            PickupPosition = world.PlayerPosition;
        }

        ShootSeconds = MathF.Max(0, ShootSeconds - elapsedSeconds);
        HurtSeconds = MathF.Max(0, HurtSeconds - elapsedSeconds);
        PickupSeconds = MathF.Max(0, PickupSeconds - elapsedSeconds);
        ElapsedSeconds += elapsedSeconds;

        var selectedState = PlayerPresentationStateSelector.Select(new PlayerPresentationInput(
            MathF.Abs(world.PlayerVelocity.X) > 0.01f,
            world.IsGrounded,
            world.PlayerVelocity.Y < 0,
            ShootSeconds > 0,
            world.IsReloading,
            HurtSeconds > 0,
            world.IsDashing));
        if (PlayerAnimationRestart.ShouldReset(PlayerState, selectedState, acceptedShot))
        {
            PlayerState = selectedState;
            playerClock.Reset();
        }
        playerClock.Advance(elapsedSeconds, FrontierAnimationCatalog.For(PlayerState));

        foreach (var enemy in world.Enemies)
        {
            var clock = enemyClocks[enemy.Id];
            clock.Advance(elapsedSeconds, enemy);
            if (enemy.Alive)
                defeatClocks.Remove(enemy.Id);
            else
            {
                if (!defeatClocks.TryGetValue(enemy.Id, out var defeatClock))
                    defeatClocks[enemy.Id] = defeatClock = new BoundedEffectClock(3, 6f);
                defeatClock.Advance(elapsedSeconds);
            }
        }
        foreach (var pickup in pickupClocks)
            pickup.Value.Advance(elapsedSeconds, pickup.Key);
    }

    public AnimationFrame PlayerFrame() =>
        playerClock.CurrentFrame(FrontierAnimationCatalog.For(PlayerState));

    public int EnemyFrame(string enemyId) => enemyClocks[enemyId].CurrentFrameIndex;
    public AnimationFrame PickupFrame(PickupType type) => pickupClocks[type].CurrentFrame();
    public BoundedEffectClock? DefeatClock(string enemyId) =>
        defeatClocks.GetValueOrDefault(enemyId);
    public int FixedFrame(int frameCount, float framesPerSecond) =>
        (int)(ElapsedSeconds * framesPerSecond) % frameCount;

    private void PrepareClocks(GameWorld world)
    {
        foreach (var enemy in world.Enemies)
        {
            var clock = new PresentationAnimationClock();
            clock.Advance(0, enemy);
            enemyClocks[enemy.Id] = clock;
            if (!enemy.Alive)
                defeatClocks[enemy.Id] = new BoundedEffectClock(3, 6f);
        }
        foreach (var pickupType in FrontierAnimationCatalog.PickupClips.Keys)
        {
            var clock = new PresentationAnimationClock();
            clock.Advance(0, pickupType);
            pickupClocks[pickupType] = clock;
        }
    }
}
