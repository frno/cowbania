namespace Cowbania.Core;

public static class FatalExceptionFormatter
{
    public static string Format(string source, object? exceptionObject, bool isTerminating)
    {
        var exceptionType = exceptionObject?.GetType().FullName ?? "<null>";
        var details =
            $"process exception source=\"{source}\" terminating={isTerminating} exceptionType=\"{exceptionType}\"{Environment.NewLine}" +
            $"exception.ToString():{Environment.NewLine}{exceptionObject}";

        if (exceptionObject is AggregateException aggregate)
        {
            var exceptions = aggregate.Flatten().InnerExceptions;
            for (var index = 0; index < exceptions.Count; index++)
            {
                details +=
                    $"{Environment.NewLine}aggregateInner[{index}].ToString():{Environment.NewLine}{exceptions[index]}";
            }
        }

        return details;
    }
}
