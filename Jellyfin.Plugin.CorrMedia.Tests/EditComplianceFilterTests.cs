using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class EditComplianceFilterTests
{
    private static CorrEdits SampleEdits()
    {
        var mutes = new List<MuteTimeRange>
        {
            new(0, 10, Id: "mute-profanity"),
            new(20, 25, Id: "mute-other")
        };
        var skips = new List<MuteTimeRange>
        {
            new(30, 40, Id: "skip-graphic")
        };
        var effects = new List<VideoEffect>
        {
            new(VideoEffectKind.Blur, 50, 55, 1, 0.5, 0.5, 8, null, Id: "blur-nudity")
        };
        var all = new List<CorrEditDescriptor>
        {
            new("mute-profanity", "mute", 0, 10, null, ["profanity"]),
            new("mute-other", "mute", 20, 25, null, []),
            new("skip-graphic", "skip", 30, 40, null, ["graphic"]),
            new("blur-nudity", "blur", 50, 55, null, ["female_nudity"])
        };
        return new CorrEdits(mutes, skips, effects, all);
    }

    [Fact]
    public void Apply_NullOverrides_ReturnsSameInstance()
    {
        var edits = SampleEdits();
        Assert.Same(edits, EditComplianceFilter.Apply(edits, null));
    }

    [Fact]
    public void Apply_Unrestricted_ReturnsSameInstance()
    {
        var edits = SampleEdits();
        var overrides = new UserItemEditOverrides { ApplyEdits = true, RestrictToCategories = false };
        Assert.Same(edits, EditComplianceFilter.Apply(edits, overrides));
    }

    [Fact]
    public void Apply_ApplyEditsOff_DropsPlaybackListsButKeepsDescriptors()
    {
        var edits = SampleEdits();
        var overrides = new UserItemEditOverrides { ApplyEdits = false };
        var filtered = EditComplianceFilter.Apply(edits, overrides);
        Assert.False(filtered.HasPlaybackEdits);
        Assert.Empty(filtered.Mutes);
        Assert.Empty(filtered.Skips);
        Assert.Empty(filtered.VideoEffects);
        Assert.Equal(edits.All, filtered.All);
    }

    [Fact]
    public void Apply_RestrictedToProfanity_HonorsOnlyMatchingEdits()
    {
        var edits = SampleEdits();
        var overrides = new UserItemEditOverrides { RestrictToCategories = true };
        overrides.EnabledCategories.Add("profanity");
        var filtered = EditComplianceFilter.Apply(edits, overrides);
        Assert.Single(filtered.Mutes);
        Assert.Equal("mute-profanity", filtered.Mutes[0].Id);
        Assert.Empty(filtered.Skips);
        Assert.Empty(filtered.VideoEffects);
        Assert.Equal(4, filtered.All.Count);
    }

    [Fact]
    public void Apply_RestrictedToOther_HonorsUntaggedEdits()
    {
        var edits = SampleEdits();
        var overrides = new UserItemEditOverrides { RestrictToCategories = true };
        overrides.EnabledCategories.Add("other");
        var filtered = EditComplianceFilter.Apply(edits, overrides);
        Assert.Single(filtered.Mutes);
        Assert.Equal("mute-other", filtered.Mutes[0].Id);
        Assert.Empty(filtered.Skips);
        Assert.Empty(filtered.VideoEffects);
    }

    [Fact]
    public void Apply_ParentCategoryCoversWordLeaves()
    {
        var mutes = new List<MuteTimeRange> { new(0, 1, Id: "word") };
        var all = new List<CorrEditDescriptor>
        {
            new("word", "mute", 0, 1, null, ["word_fuck"])
        };
        var edits = new CorrEdits(mutes, [], [], all);
        var overrides = new UserItemEditOverrides { RestrictToCategories = true };
        overrides.EnabledCategories.Add("profanity");
        var filtered = EditComplianceFilter.Apply(edits, overrides);
        Assert.Single(filtered.Mutes);
    }
}
