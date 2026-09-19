using System.Text.RegularExpressions;

namespace Cowbania.Host.Tests.Harness;

internal static class SourceInspection
{
    public static string MethodBody(string source, string methodName)
    {
        var declaration = Regex.Match(
            source,
            $@"(?m)^\s+(?:private|protected|public)\s+[^\r\n]*\b{Regex.Escape(methodName)}\s*\(");
        Assert(declaration.Success, $"cannot find method {methodName}");
        var nextDeclaration = Regex.Match(
            source[(declaration.Index + declaration.Length)..],
            @"(?m)^\s+(?:private|protected|public)\s+");
        var end = nextDeclaration.Success
            ? declaration.Index + declaration.Length + nextDeclaration.Index
            : source.Length;
        return source[declaration.Index..end];
    }

    public static void AssertInOrder(string source, params string[] markers)
    {
        var previous = -1;
        foreach (var marker in markers)
        {
            var index = source.IndexOf(marker, previous + 1, StringComparison.Ordinal);
            Assert(index > previous, $"render marker '{marker}' is missing or out of depth order");
            previous = index;
        }
    }
}
