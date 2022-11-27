using System.Reflection;
using System.Text.RegularExpressions;
using CliWrap;
using diffcalc_comparer.Utils;
using osu.Game.Rulesets;
using Spectre.Console;

namespace diffcalc_comparer;

public static class Program
{
    private static string rulesetAUrl = null!;
    private static string rulesetBUrl = null!;
    private static string rulesetAPath = null!;
    private static string rulesetBPath = null!;

    public static void Main(string[] args)
    {
        loadEnv();

        rulesetAUrl = Environment.GetEnvironmentVariable("RULESET_A") ?? throw new Exception("Environment variable \"RULESET_A\" must be defined.");
        rulesetBUrl = Environment.GetEnvironmentVariable("RULESET_B") ?? throw new Exception("Environment variable \"RULESET_B\" must be defined.");
        rulesetAPath = Path.Combine(Directory.GetCurrentDirectory(), "ruleset_a");
        rulesetBPath = Path.Combine(Directory.GetCurrentDirectory(), "ruleset_b");

        cloneRepos().Wait();
        buildAndLoadRulesets().Wait();

        var includeUrls = AnsiConsole.Confirm("embed url in beatmap name?", false);
        var exportPath = AnsiConsole.Prompt(new TextPrompt<string?>("[[optional]] Export path?").AllowEmpty());

        new DiffComparer(new DiffComparerOptions
        {
            ExportPath = exportPath,
            IncludeUrl = includeUrls
        }).Compare();
    }

    private static Task buildAndLoadRulesets()
    {
        return AnsiConsole.Status()
           .StartAsync("Building rulesets", async ctx =>
            {
                var rulesetName = getRulesetName();
                ctx.Status = $"Building {rulesetName} rulesets (variant A)";
                ctx.Spinner(Spinner.Known.Circle);
                Env.RulesetA = await buildAndLoadRuleset(rulesetAPath, "A");

                ctx.Status = $"Building {rulesetName} rulesets (variant B)";
                Env.RulesetB = await buildAndLoadRuleset(rulesetBPath, "B");

                async Task<Ruleset> buildAndLoadRuleset(string rulesetPath, string variant)
                {
                    await Cli.Wrap("dotnet")
                       .WithArguments(new[] { "build", rulesetName, "-c", "Release" }!)
                       .WithWorkingDirectory(rulesetPath)
                       .ExecuteWithLogging();

                    var buildPath = rulesetPath;
                    if (Directory.Exists(Path.Combine(buildPath, rulesetName)))
                        buildPath = Path.Combine(buildPath, rulesetName);
                    buildPath = Path.Combine(buildPath, "bin", "Release");
                    var netVersion = Directory.GetDirectories(buildPath)[0];
                    buildPath = Path.Combine(buildPath, netVersion);
                    var dllName = rulesetName + ".dll";

                    var rulesetAssembly = Assembly.LoadFile(Path.Combine(buildPath, dllName));
                    Ruleset rulesetClass;

                    try
                    {
                        var rulesetType = rulesetAssembly.GetTypes().First(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset)));
                        rulesetClass = Activator.CreateInstance(rulesetType) as Ruleset ?? throw new InvalidOperationException();
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Could not load ruleset {rulesetName} (variant {variant}).", e);
                    }

                    if (rulesetClass.RulesetAPIVersionSupported != Ruleset.CURRENT_RULESET_API_VERSION)
                        throw new Exception(
                            $"Ruleset version is out of date. ({rulesetName} (variant {variant}): {rulesetClass.RulesetAPIVersionSupported} | current: {Ruleset.CURRENT_RULESET_API_VERSION})");

                    return rulesetClass;
                }
            });
    }

    private static string getRulesetName()
    {
        string rulesetName = null!;
        var rulesetPathRegex = new Regex(@"osu\.Game\.Rulesets\.\w{1,}");

        foreach (var directory in Directory.GetDirectories(rulesetAPath))
        {
            var match = rulesetPathRegex.Match(directory);
            if (!match.Success)
                continue;

            rulesetName = match.Value;
            break;
        }

        if (!string.IsNullOrEmpty(rulesetName))
            return rulesetName;

        var rulesetFileRegex = new Regex(@"osu\.Game\.Rulesets\.\w{1,}(\.csproj|\.sln)");

        foreach (var file in Directory.GetFiles(rulesetAPath))
        {
            var match = rulesetFileRegex.Match(file);
            if (!match.Success)
                continue;

            rulesetName = match.Groups[1].Value;
            break;
        }

        return rulesetName;
    }

    private static Task cloneRepos()
    {
        return AnsiConsole.Status()
           .StartAsync($"Cloning \"{rulesetAUrl}\" -> {rulesetAPath}", async ctx =>
            {
                await Github.CloneRepo(rulesetAUrl!, rulesetAPath);
                ctx.Status = $"Cloning \"{rulesetBUrl}\" -> {rulesetBPath}";
                await Github.CloneRepo(rulesetBUrl!, rulesetBPath);
            });
    }

    private static void loadEnv()
    {
        var filePath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.FullName, ".env");

        if (!File.Exists(filePath))
            throw new Exception($"Could not find a \".env\" file. Make sure to copy the \".env.example\" from the root directory to \"{filePath}\"");

        foreach (var line in File.ReadAllLines(filePath))
        {
            var split = line.Split('=', StringSplitOptions.RemoveEmptyEntries);

            if (split.Length != 2)
                continue;

            Environment.SetEnvironmentVariable(split[0], split[1]);
        }
    }
}
