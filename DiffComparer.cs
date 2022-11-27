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
    private DiffComparerOptions options;

    public DiffComparer(DiffComparerOptions options)
    {
        this.options = options;
    }

    public void Compare()
    {
        var beatmaps = loadBeatmaps();
        var rows = new List<string[]>();
        var table = new Table();

        table.AddColumn("Title");
        table.AddColumn(new TableColumn("SR (A)").Centered());
        table.AddColumn(new TableColumn("SR (B)").Centered());
        table.AddColumn(new TableColumn("DIFF").Centered());
        table.AddColumn(new TableColumn("AP (A)").Centered());
        table.AddColumn(new TableColumn("AP (B)").Centered());
        table.AddColumn(new TableColumn("RX (A)").Centered());
        table.AddColumn(new TableColumn("RX (B)").Centered());

        foreach (var beatmap in beatmaps)
        {
            var srA = getStarRatings(beatmap, Env.RulesetA);
            var srB = getStarRatings(beatmap, Env.RulesetB);

            var diff = srB.sr - srA.sr < 0 ? (srB.sr - srA.sr).ToString("N") : "+" + (srB.sr - srA.sr).ToString("N");
            var title = options.IncludeUrl.HasValue && options.IncludeUrl.Value
                            ? $"[{beatmap}](https://osu.ppy.sh/b/{beatmap.BeatmapInfo.OnlineID})"
                            : beatmap.ToString();

            rows.Add(new[]
            {
                title,
                srA.sr.ToString("N"), srB.sr.ToString("N"),
                diff,
                srA.ap.HasValue ? srA.ap.Value.ToString("N") : "-", srB.ap.HasValue ? srB.ap.Value.ToString("N") : "-",
                srA.rx.HasValue ? srA.rx.Value.ToString("N") : "-", srB.rx.HasValue ? srB.rx.Value.ToString("N") : "-"
            });

            table.AddRow(beatmap.ToString().EscapeMarkup(),
                srA.sr.ToString("N"), srB.sr.ToString("N"),
                diff,
                srA.ap.HasValue ? srA.ap.Value.ToString("N") : "-", srB.ap.HasValue ? srB.ap.Value.ToString("N") : "-",
                srA.rx.HasValue ? srA.rx.Value.ToString("N") : "-", srB.rx.HasValue ? srB.rx.Value.ToString("N") : "-");
        }

        AnsiConsole.Write(table);

        if (!string.IsNullOrEmpty(options.ExportPath))
        {
            var sb = new StringBuilder();

            sb.AppendLine("| Title | SR (A) | SR (B) | DIFF | AP (A) | AP (B) | RX (A) | RX (B) |");
            sb.AppendLine("|-------|:------:|:------:|:----:|:------:|:------:|:------:|:------:|");

            foreach (var row in rows)
            {
                sb.AppendLine($"| {row[0]} | {row[1]} | {row[2]} | {row[3]} | {row[4]} | {row[5]} | {row[6]} | {row[7]} |");
            }

            File.WriteAllText(options.ExportPath, sb.ToString());
        }
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

        foreach (var file in Directory.GetFiles(mapsPath))
        {
            if (file == ".gitignore" || !file.EndsWith(".osu"))
                continue;

            var legacyDecoder = new LegacyBeatmapDecoder();
            beatmaps.Add(legacyDecoder.Decode(new LineBufferedReader(File.Open(Path.Combine(mapsPath, file), FileMode.Open))));
        }

        return beatmaps;
    }
}

public struct DiffComparerOptions
{
    public string? ExportPath;
    public bool? IncludeUrl;
}
