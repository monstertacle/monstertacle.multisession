using System.Diagnostics;
using System.Reflection;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>
/// T023 SDK004: the validation host's real entry point routes the first-line fixture worker argument to that worker. It runs in the
/// collection that owns console redirection, so its observable output can be captured without affecting other tests.
/// </summary>
[Collection(WorkerConsoleIsolation.Name)]
public sealed class FirstLineEntryDispatchTests
{
    /// <summary>
    /// The compiler's synchronous wrapper of <c>async Task&lt;int&gt; Main</c>, called with the worker argument and an unknown
    /// variant, returns 2 at once with no standard output or error and no process-id record. If the dispatch arm were missing, the
    /// scenario runner would take the arguments and write its report.
    /// </summary>
    [Fact]
    public void EntryPointRoutesTheFirstLineWorkerArgumentToItsRefusal()
    {
        MethodInfo entry = typeof(FirstLineFixtureWorker).Assembly.EntryPoint ?? throw new InvalidOperationException("no entry point");
        Assert.True(entry.IsStatic);
        Assert.Equal(typeof(int), entry.ReturnType); // the synchronous wrapper, not the async Main itself
        string record = Path.Combine(Environment.CurrentDirectory, FirstLineFixtureProtocol.ProcessIdFileName);
        bool recordedBefore = File.Exists(record);
        TextWriter originalOutput = Console.Out, originalError = Console.Error;
        using StringWriter output = new(), error = new();
        object? exit;
        long began = Stopwatch.GetTimestamp();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            exit = entry.Invoke(null, [new[] { FirstLineFixtureProtocol.WorkerArgument, "unknown" }]);
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
        Assert.Equal(2, exit);
        Assert.Empty(output.ToString());
        Assert.Empty(error.ToString());
        Assert.Equal(recordedBefore, File.Exists(record));
        Assert.True(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(10), "the refusal must not wait for the worker's self limit");
    }
}
