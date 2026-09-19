using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace Cowbania.Host.Diagnostics;

internal sealed class FrameTelemetry
{
    private readonly Stopwatch runtimeClock = Stopwatch.StartNew();
    private long updateFrames;
    private long drawFrames;
    private double lastUpdateHeartbeat = -2;
    private double lastDrawHeartbeat = -2;
    private double lastUpdateDuration;
    private double lastDrawDuration;
    private bool firstUpdate;
    private bool firstDraw;

    public long BeginUpdate(GameTime gameTime, GameWorld world)
    {
        updateFrames++;
        var now = runtimeClock.Elapsed.TotalSeconds;
        if (now - lastUpdateHeartbeat >= 2)
        {
            lastUpdateHeartbeat = now;
            RuntimeLog.Info(
                $"Update heartbeat begin frame={updateFrames} runtimeSeconds={now:F1} " +
                $"gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} previousExecutionMs={lastUpdateDuration:F1} " +
                $"room={world.Room} objective={world.ObjectivePhase} paused={world.IsPaused} completed={world.Completed} " +
                $"health={world.Health} ammo={world.Ammo} enemies={world.Enemies.Count(enemy => enemy.Alive)} " +
                $"projectiles={world.Projectiles.Count} hostileProjectiles={world.Projectiles.Count(projectile => projectile.Owner == ProjectileOwner.Enemy)}");
        }
        return Stopwatch.GetTimestamp();
    }

    public void EndUpdate(long started, GameTime gameTime, GameWorld world)
    {
        lastUpdateDuration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (gameTime.ElapsedGameTime.TotalSeconds >= 0.1 || lastUpdateDuration >= 50)
            RuntimeLog.Warn(
                $"slow Update frame={updateFrames} gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} " +
                $"executionMs={lastUpdateDuration:F1} room={world.Room} paused={world.IsPaused}");
    }

    public long BeginDraw(GameTime gameTime, GameWorld world)
    {
        drawFrames++;
        var now = runtimeClock.Elapsed.TotalSeconds;
        if (now - lastDrawHeartbeat >= 2)
        {
            lastDrawHeartbeat = now;
            RuntimeLog.Info(
                $"Draw heartbeat begin frame={drawFrames} runtimeSeconds={now:F1} " +
                $"gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} previousExecutionMs={lastDrawDuration:F1} " +
                $"updateFrame={updateFrames} room={world.Room}");
        }
        return Stopwatch.GetTimestamp();
    }

    public void EndDraw(long started, GameTime gameTime, GameWorld world)
    {
        lastDrawDuration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (lastDrawDuration >= 50)
            RuntimeLog.Warn(
                $"slow Draw frame={drawFrames} gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} " +
                $"executionMs={lastDrawDuration:F1} room={world.Room}");
    }

    public bool MarkFirstUpdate() => !firstUpdate && (firstUpdate = true);
    public bool MarkFirstDraw() => !firstDraw && (firstDraw = true);
}
