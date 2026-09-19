using System.Reflection;

namespace Cowbania.Core.Tests.Harness;

internal static class ReflectionAccess
{
    public static T GetField<T>(object target, string fieldName)
    {
        var (owner, field) = FindField(target, fieldName);
        if (field is null)
            throw new InvalidOperationException($"Missing field '{fieldName}' on {target.GetType().Name}.");
        return (T)field.GetValue(owner)!;
    }

    public static void SetField<T>(object target, string fieldName, T value)
    {
        var (owner, field) = FindField(target, fieldName);
        if (field is null)
            throw new InvalidOperationException($"Missing field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(owner, value);
    }

    public static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (property?.SetMethod is null)
            throw new InvalidOperationException($"Missing property '{propertyName}' on {target.GetType().Name}.");
        property.SetValue(target, value);
    }

    public static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method is not null)
        {
            method.Invoke(target, null);
            return;
        }

        if (target is GameWorld game)
        {
            var state = GetField<object>(game, "state");
            var progression = game.GetType().Assembly.GetType("Cowbania.Core.Gameplay.World.WorldProgressionSystem");
            var extractedMethod = progression?.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (extractedMethod is not null)
            {
                extractedMethod.Invoke(null, [state]);
                return;
            }
        }

        throw new InvalidOperationException($"Missing method '{methodName}' on {target.GetType().Name}.");
    }

    private static (object Owner, FieldInfo? Field) FindField(object target, string fieldName)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var direct = target.GetType().GetField(fieldName, flags);
        if (direct is not null)
            return (target, direct);
        if (target is not GameWorld)
            return (target, null);

        var stateField = target.GetType().GetField("state", flags)!;
        var state = stateField.GetValue(target)!;
        var stateName = char.ToUpperInvariant(fieldName[0]) + fieldName[1..];
        return (state, state.GetType().GetField(stateName, flags));
    }
}
