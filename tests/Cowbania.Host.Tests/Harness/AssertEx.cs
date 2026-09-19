namespace Cowbania.Host.Tests.Harness;

internal static class AssertEx
{
    public static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, found {actual}");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException(message);
    }

    public static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        throw new InvalidOperationException(message);
    }

    public static int Count(string value, string marker)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += marker.Length;
        }
        return count;
    }
}
