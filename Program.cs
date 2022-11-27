using diffcalc_comparer.Utils;
using Spectre.Console;

namespace diffcalc_comparer;

public static class Program
{
    public static void Main(string[] args)
    {
        loadEnv();
        cloneRepos();
    }

    private static void cloneRepos()
    {
        if (Environment.GetEnvironmentVariable("RULESET_A") == null)
            throw new Exception("Environment variable \"RULESET_A\" must be defined.");
        if (Environment.GetEnvironmentVariable("RULESET_B") == null)
            throw new Exception("Environment variable \"RULESET_B\" must be defined.");

        var rulesetAUrl = Environment.GetEnvironmentVariable("RULESET_A");
        var rulesetBUrl = Environment.GetEnvironmentVariable("RULESET_B");
        var rulesetAPath = Path.Combine(Directory.GetCurrentDirectory(), "ruleset_a");
        var rulesetBPath = Path.Combine(Directory.GetCurrentDirectory(), "ruleset_b");

        AnsiConsole.Status()
           .StartAsync($"Cloning \"{rulesetAUrl}\" -> {rulesetAPath}", async ctx =>
            {
                await Github.CloneRepo(rulesetAUrl!, rulesetAPath);
                ctx.Status = $"Cloning \"{rulesetBUrl}\" -> {rulesetBPath}";
                await Github.CloneRepo(rulesetBUrl!, rulesetBPath);
            }).Wait();
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
