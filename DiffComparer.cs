using System.Diagnostics.CodeAnalysis;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using Spectre.Console;

namespace diffcalc_comparer;

public class DiffComparer
{
    private readonly DiffComparerOptions options;

    public DiffComparer(DiffComparerOptions options)
    {
        this.options = options;
    }

    public void Compare()
    {
        var beatmaps = loadBeatmaps().ToArray();

        var rows = new List<BeatmapCalculationResult>();
        var table = new Table();

        table.AddColumn("Title");
        table.AddColumn(new TableColumn("SR (A)").Centered());
        table.AddColumn(new TableColumn("SR (B)").Centered());
        table.AddColumn(new TableColumn("DIFF").Centered());
        table.AddColumn(new TableColumn("AP (A)").Centered());
        table.AddColumn(new TableColumn("AP (B)").Centered());
        table.AddColumn(new TableColumn("RX (A)").Centered());
        table.AddColumn(new TableColumn("RX (B)").Centered());

        AnsiConsole.Progress()
           .Start(ctx =>
            {
                var sraTask = ctx.AddTask($"Calculate star ratings (Variant A)", true, beatmaps.Length);
                var srbTask = ctx.AddTask($"Calculate star ratings (Variant B)", true, beatmaps.Length);
                
                foreach (var beatmap in beatmaps)
                {
                    var srA = getStarRatings(beatmap, Env.RulesetA);
                    sraTask.Increment(1);
                    var srB = getStarRatings(beatmap, Env.RulesetB);
                    srbTask.Increment(1);

                    rows.Add(new BeatmapCalculationResult
                    {
                        Beatmap = beatmap,
                        SRA = srA.sr,
                        SRB = srB.sr,
                        Diff = srB.sr - srA.sr,
                        APA = srA.ap,
                        APB = srB.ap,
                        RXA = srA.rx,
                        RXB = srB.rx
                    });
                }
            });

        var orderedRows = rows.OrderByDescending(r => r.Diff).ToArray();

        foreach (var row in orderedRows)
        {
            table.AddRow(row.Beatmap.ToString().EscapeMarkup(),
                row.SRA.ToString("N"), row.SRB.ToString("N"),
                row.Diff < 0 ? row.Diff.ToString("N") : "+" + row.Diff.ToString("N"),
                row.APA.HasValue ? row.APA.Value.ToString("N") : "-", row.APB.HasValue ? row.APB.Value.ToString("N") : "-",
                row.RXA.HasValue ? row.RXA.Value.ToString("N") : "-", row.RXB.HasValue ? row.RXB.Value.ToString("N") : "-");
        }

        AnsiConsole.Write(table);

        if (string.IsNullOrEmpty(options.ExportPath))
            return;

        var sb = new StringBuilder();

        sb.AppendLine("| Title | SR (A) | SR (B) | DIFF | AP (A) | AP (B) | RX (A) | RX (B) |");
        sb.AppendLine("|-------|:------:|:------:|:----:|:------:|:------:|:------:|:------:|");

        foreach (var row in orderedRows)
        {
            var title = options.IncludeUrl.HasValue && options.IncludeUrl.Value
                            ? $"[{row.Beatmap}](https://osu.ppy.sh/b/{row.Beatmap.BeatmapInfo.OnlineID})"
                            : row.Beatmap.ToString();
                
            sb.AppendLine($"| {title} | {row.SRA} | {row.SRB} | {row.Diff} | {row.APA} | {row.APB} | {row.RXA} | {row.RXB} |");
        }

        File.WriteAllText(options.ExportPath, sb.ToString());
    }

    private static (double sr, double? ap, double? rx) getStarRatings(Beatmap beatmap, Ruleset ruleset)
    {
        var convert = ruleset.CreateBeatmapConverter(beatmap).Convert();
        var calculator = ruleset.CreateDifficultyCalculator(new CalcWorkingBeatmap(ruleset, convert));

        var sr = calculator.Calculate().StarRating;
        double? ap = null!;
        double? rx = null!;

        try
        {
            var autoplayMod = ruleset.AllMods.First(m => m.GetType().IsSubclassOf(typeof(ModAutoplay))) as Mod;
            ap = calculator.Calculate(new[] { autoplayMod }).StarRating;
        }
        catch
        {
            // ignore
        }

        try
        {
            var relaxMod = ruleset.AllMods.First(m => m.GetType().IsSubclassOf(typeof(ModRelax))) as Mod;
            rx = calculator.Calculate(new[] { relaxMod }).StarRating;
        }
        catch
        {
            // ignore
        }

        return (sr, ap, rx);
    }

    private static IEnumerable<Beatmap> loadBeatmaps()
    {
        var mapsPath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.FullName, "maps");
        var beatmaps = new List<Beatmap>();

        AnsiConsole.Progress()
           .Start(ctx =>
            {
                var length = Directory.GetFiles(mapsPath).Length;
                var task = ctx.AddTask($"Parse osu beatmaps", true, length);

                foreach (var file in Directory.GetFiles(mapsPath))
                {
                    if (file == ".gitignore" || !file.EndsWith(".osu"))
                        continue;

                    var legacyDecoder = new LegacyBeatmapDecoder();
                    beatmaps.Add(legacyDecoder.Decode(new LineBufferedReader(File.Open(Path.Combine(mapsPath, file), FileMode.Open))));
                    task.Increment(1);
                }
            });

        return beatmaps;
    }
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private class BeatmapCalculationResult
    {
        public Beatmap Beatmap { get; init; } = null!;
        public double SRA { get; init; }
        public double SRB { get; init; }
        public double Diff { get; init; }
        public double? APA { get; init; }
        public double? APB { get; init; }
        public double? RXA { get; init; }
        public double? RXB { get; init; }
    }
}

public struct DiffComparerOptions
{
    public string? ExportPath;
    public bool? IncludeUrl;
}
