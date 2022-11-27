using System.Text.RegularExpressions;
using CliWrap;
using Spectre.Console;

namespace diffcalc_comparer.Utils;

public static class Github
{
    public static async Task CloneRepo(string url, string targetDir)
    {
        var githubRegex = new Regex(@"^(https:\/\/github\.com\/[^\/]+\/[^\/]+)\/?([^\/]+)?\/?([^\/]+)?(\/commits\/(.*))?$");

        var match = githubRegex.Match(url);
        if (!match.Success)
            throw new InvalidOperationException($"Couldn't clone {url} as it's an invalid URL.");

        var repo = match.Groups[1].Value;
        var target = match.Groups[2].Value;
        var targetInfo = match.Groups[3].Value;
        var prCommit = match.Groups[5].Value;

        var cloneResult = await Cli.Wrap("git")
                             .WithArguments(new[] { "clone", "--filter=tree:0", repo, targetDir })
                             .WithValidation(CommandResultValidation.None)
                             .ExecuteWithLogging();

        if (cloneResult.ExitCode == 0)
            AnsiConsole.MarkupLineInterpolated($"[dim grey]LOG:[/] Successfully cloned {url}");

        switch (target)
        {
            case "pull":
                var prCheckoutResult = await Cli.Wrap("gh")
                                          .WithArguments(new[] { "pr", "checkout", targetInfo })
                                          .WithWorkingDirectory(targetDir)
                                          .ExecuteWithLogging();

                if (prCheckoutResult.ExitCode == 0)
                    AnsiConsole.MarkupLineInterpolated($"[dim grey]LOG:[/] Successfully checked out pr {targetInfo}");

                if (!string.IsNullOrEmpty(prCommit))
                {
                    var prCheckoutResult2 = await Cli.Wrap("git")
                                               .WithArguments(new[] { "checkout", prCommit })
                                               .WithWorkingDirectory(targetDir)
                                               .ExecuteWithLogging();

                    if (prCheckoutResult2.ExitCode == 0)
                        AnsiConsole.MarkupLineInterpolated($"[dim grey]LOG:[/] Successfully checked out {prCommit}");
                }

                break;

            case "commit":
            case "tree":
                var checkoutResult = await Cli.Wrap("git")
                                        .WithArguments(new[] { "checkout", targetInfo })
                                        .WithWorkingDirectory(targetDir)
                                        .ExecuteWithLogging();

                if (checkoutResult.ExitCode == 0)
                    AnsiConsole.MarkupLineInterpolated($"[dim grey]LOG:[/] Successfully checked out {targetInfo}");
                break;

            case "":
                break;

            default:
                throw new Exception($"Not a valid repository url: {url}");
        }
    }
}
