namespace Cowbania.Host.Tests.Harness;

internal static class TestRunner
{
    public static void Run(IEnumerable<TestCase> cases)
    {
        var failures = new List<string>();
        foreach (var test in cases)
        {
            try
            {
                test.Execute();
                Console.WriteLine($"[PASS] {test.Name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{test.Name}: {exception.Message}");
                Console.WriteLine($"[FAIL] {test.Name}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Cowbania.Host.Tests: FAIL ({failures.Count}){Environment.NewLine}" +
                string.Join(Environment.NewLine, failures.Select(failure => $"- {failure}")));

        Console.WriteLine($"Cowbania.Host.Tests: PASS ({TestInventory.ExpectedNames.Length})");
    }
}
