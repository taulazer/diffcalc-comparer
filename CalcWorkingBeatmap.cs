using osu.Framework.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps;

namespace diffcalc_comparer;

public class CalcWorkingBeatmap : TestWorkingBeatmap
{
    private readonly Ruleset rulesetInstance;

    public CalcWorkingBeatmap(Ruleset rulesetInstance, IBeatmap beatmap, Storyboard? storyboard = null, AudioManager? audioManager = null)
        : base(beatmap, storyboard, audioManager)
    {
        this.rulesetInstance = rulesetInstance;
    }

    // Copied function from https://github.com/ppy/osu/blob/61bfd2f6b2a94d54b7b4cff574e34ad4c06eb457/osu.Game/Beatmaps/WorkingBeatmap.cs#L245
    // but without ruleset instantiation.
    public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
    {
        var converter = CreateBeatmapConverter(Beatmap, rulesetInstance);

        // Check if the beatmap can be converted
        if (Beatmap.HitObjects.Count > 0 && !converter.CanConvert())
            throw new BeatmapInvalidForRulesetException(
                $"{nameof(Beatmap)} can not be converted for the ruleset (ruleset: {ruleset.InstantiationInfo}, converter: {converter}).");

        // Apply conversion mods
        foreach (var mod in mods.OfType<IApplicableToBeatmapConverter>())
        {
            token.ThrowIfCancellationRequested();
            mod.ApplyToBeatmapConverter(converter);
        }

        // Convert
        var converted = converter.Convert(token);

        // Apply conversion mods to the result
        foreach (var mod in mods.OfType<IApplicableAfterBeatmapConversion>())
        {
            token.ThrowIfCancellationRequested();
            mod.ApplyToBeatmap(converted);
        }

        // Apply difficulty mods
        if (mods.Any(m => m is IApplicableToDifficulty))
            foreach (var mod in mods.OfType<IApplicableToDifficulty>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToDifficulty(converted.Difficulty);
            }

        var processor = rulesetInstance.CreateBeatmapProcessor(converted);

        if (processor != null)
        {
            foreach (var mod in mods.OfType<IApplicableToBeatmapProcessor>())
                mod.ApplyToBeatmapProcessor(processor);

            processor.PreProcess();
        }

        // Compute default values for hitobjects, including creating nested hitobjects in-case they're needed
        foreach (var obj in converted.HitObjects)
        {
            token.ThrowIfCancellationRequested();
            obj.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty, token);
        }

        foreach (var mod in mods.OfType<IApplicableToHitObject>())
        foreach (var obj in converted.HitObjects)
        {
            token.ThrowIfCancellationRequested();
            mod.ApplyToHitObject(obj);
        }

        processor?.PostProcess();

        foreach (var mod in mods.OfType<IApplicableToBeatmap>())
        {
            token.ThrowIfCancellationRequested();
            mod.ApplyToBeatmap(converted);
        }

        return converted;
    }
}
