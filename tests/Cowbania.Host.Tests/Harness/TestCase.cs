namespace Cowbania.Host.Tests.Harness;

internal readonly record struct TestCase(string Name, Action Execute);
