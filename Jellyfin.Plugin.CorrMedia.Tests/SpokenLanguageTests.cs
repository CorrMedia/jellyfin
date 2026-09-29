using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class SpokenLanguageTests
{
    [Theory]
    [InlineData("en", "eng")]
    [InlineData("ENG", "en")]
    [InlineData("es", "spa")]
    [InlineData("fre", "fra")]
    [InlineData("fra", "fr")]
    [InlineData("ger", "deu")]
    [InlineData("chi", "zh")]
    [InlineData("zh-Hans", "zho")]
    public void Same_TreatsIsoAliasesAsOneLanguage(string left, string right)
    {
        Assert.True(SpokenLanguage.Same(left, right));
    }

    [Fact]
    public void Same_RejectsDifferentLanguagesAndUndeterminedCodes()
    {
        Assert.False(SpokenLanguage.Same("eng", "spa"));
        Assert.False(SpokenLanguage.Same("eng", "und"));
        Assert.False(SpokenLanguage.Same("eng", "mul"));
        Assert.False(SpokenLanguage.Same("xyz", "eng"));
        Assert.False(SpokenLanguage.Same(null, "eng"));
    }

    [Fact]
    public void Applies_KeepsUntaggedEditsAndDropsTaggedEditsOnAnUntaggedStream()
    {
        Assert.True(SpokenLanguage.Applies(null, "spa"));
        Assert.True(SpokenLanguage.Applies("  ", "eng"));
        Assert.True(SpokenLanguage.Applies("en", "eng"));
        Assert.False(SpokenLanguage.Applies("spa", null));
        Assert.False(SpokenLanguage.Applies("spa", "und"));
        Assert.False(SpokenLanguage.Applies("not-a-language", "eng"));
    }
}
