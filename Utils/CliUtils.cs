using CliWrap;
using Spectre.Console;

namespace diffcalc_comparer.Utils;

public static class CliUtils
{
    public static CommandTask<CommandResult> ExecuteWithLogging(this Command command)
        => command
           .WithStandardOutputPipe(PipeTarget.ToDelegate(log))
           .WithStandardErrorPipe(PipeTarget.ToDelegate(log))
           .ExecuteAsync();

    private static void log(string s)
    {
        AnsiConsole.MarkupLineInterpolated($"[dim grey]LOG:[/] {s}");
    }
}
