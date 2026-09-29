using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class SpokenLanguageFilterTests
{
    [Fact]
    public void Apply_KeepsUntaggedEditsAndTheMatchingLanguage()
    {
        var edits = Sample();
        var filtered = SpokenLanguageFilter.Apply(edits, "spa");
        Assert.Equal(["es", "any"], filtered.All.Select(edit => edit.Id).ToArray());
        Assert.Equal(["any"], filtered.Skips.Select(skip => skip.Id).ToArray());
        Assert.Equal(["es"], filtered.Mutes.Select(mute => mute.Id).ToArray());
    }

    [Fact]
    public void Apply_DropsTaggedEditsWhenTheStreamLanguageIsMissing()
    {
        var filtered = SpokenLanguageFilter.Apply(Sample(), null);
        Assert.Equal(["any"], filtered.All.Select(edit => edit.Id).ToArray());
    }

    [Fact]
    public void Apply_ThenCategoryFilter_DropsALanguageMatchTheUserTurnedOff()
    {
        var forLanguage = SpokenLanguageFilter.Apply(Sample(), "en");
        var overrides = new UserItemEditOverrides { RestrictToCategories = true };
        overrides.EnabledCategories.Add("other");
        var filtered = EditComplianceFilter.Apply(forLanguage, overrides);
        Assert.DoesNotContain(filtered.All, edit => edit.Id == "es");
        Assert.Empty(filtered.Mutes);
        Assert.Equal(["any"], filtered.Skips.Select(skip => skip.Id).ToArray());
    }

    private static CorrEdits Sample()
    {
        var mutes = new List<MuteTimeRange>
        {
            new(1, 2, Id: "en"),
            new(3, 4, Id: "es")
        };
        var skips = new List<MuteTimeRange> { new(5, 6, Id: "any") };
        var all = new List<CorrEditDescriptor>
        {
            new("en", "mute", 1, 2, null, ["word_damn"], "eng"),
            new("es", "mute", 3, 4, null, ["word_damn"], "spa"),
            new("any", "skip", 5, 6, null, [])
        };
        return new CorrEdits(mutes, skips, [], all);
    }
}
