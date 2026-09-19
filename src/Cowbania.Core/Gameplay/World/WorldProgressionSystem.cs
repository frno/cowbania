using System.Numerics;
using Cowbania.Core.Gameplay.Enemies;

namespace Cowbania.Core.Gameplay.World;

internal static class WorldProgressionSystem
{
    internal static void Interact(GameWorldState state)
    {
        var room = state.CurrentRoom;
        if (state.Room == RoomCatalog.Branch.Id &&
            Vector2.Distance(state.PlayerPosition, room.Checkpoint) < GameWorld.InteractionRadius)
        {
            state.CheckpointActivated = true;
            state.CheckpointRoom = RoomCatalog.Branch.Id;
            state.CheckpointPosition = room.Checkpoint;
        }

        if (state.Room == RoomCatalog.Branch.Id &&
            Vector2.Distance(state.PlayerPosition, room.Shortcut) < GameWorld.InteractionRadius)
        {
            state.ShortcutUnlocked = true;
            state.Room = RoomCatalog.Hub.Id;
            ClearTransientEncounterState(state);
            ResetEncounter(state, state.Room);
            state.PlayerPosition = RoomCatalog.Hub.Shortcut;
            state.PlayerVelocity = Vector2.Zero;
            state.CheckpointActivated = false;
            state.CheckpointRoom = RoomCatalog.Hub.Id;
            state.CheckpointPosition = RoomCatalog.Hub.Checkpoint;
        }
        else if (state.Room == RoomCatalog.Hub.Id &&
                 state.ShortcutUnlocked &&
                 Vector2.Distance(state.PlayerPosition, RoomCatalog.Hub.Shortcut) <
                 GameWorld.InteractionRadius)
        {
            state.Completed = true;
        }
        else if (state.Room == RoomCatalog.Hub.Id &&
                 state.PlayerPosition.X > room.Bounds.Right - 40)
        {
            state.Room = RoomCatalog.Branch.Id;
            ClearTransientEncounterState(state);
            ResetEncounter(state, state.Room);
            state.PlayerPosition = RoomCatalog.Branch.Spawn;
            state.PlayerVelocity = Vector2.Zero;
        }
    }

    internal static void Respawn(GameWorldState state)
    {
        state.Health = GameWorld.MaximumHealth;
        state.Room = state.CheckpointActivated ? RoomCatalog.Branch.Id : state.CheckpointRoom;
        state.PlayerPosition = state.CheckpointPosition;
        state.PlayerVelocity = Vector2.Zero;
        ClearTransientEncounterState(state);
        ResetEncounter(state, state.Room);
    }

    private static void ClearTransientEncounterState(GameWorldState state) =>
        state.Projectiles.Clear();

    private static void ResetEncounter(GameWorldState state, int roomId)
    {
        var room = RoomCatalog.ForId(roomId);
        state.EnemiesByRoom[roomId] = room.EnemyDefinitions
            .Select(definition => new EnemyRuntime(definition))
            .ToList();
    }
}
