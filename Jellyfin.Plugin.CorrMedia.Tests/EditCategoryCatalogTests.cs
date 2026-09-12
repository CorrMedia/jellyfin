using System.IO;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class EditCategoryCatalogTests
{
    [Theory]
    [InlineData(null, "other")]
    [InlineData("", "other")]
    [InlineData("unknown_token", "other")]
    [InlineData("test", "other")]
    [InlineData("profanity", "profanity")]
    [InlineData("fuck", "word_fuck")]
    [InlineData("f_word", "word_fuck")]
    [InlineData("graphic_violence", "graphic")]
    [InlineData("kiss", "kissing_heterosexual_normal")]
    [InlineData("credits", "opening_credits")]
    public void Resolve_MapsSidecarTokens(string? raw, string expected)
    {
        Assert.Equal([expected], EditCategoryCatalog.Resolve(raw));
    }

    [Fact]
    public void Resolve_ViolenceAlias_ExpandsToMultipleMinors()
    {
        Assert.Equal(["gore", "graphic", "non_graphic"], EditCategoryCatalog.Resolve("violence"));
    }

    [Fact]
    public void ResolveAll_Empty_IsOther()
    {
        Assert.Equal(["other"], EditCategoryCatalog.ResolveAll(null));
        Assert.Equal(["other"], EditCategoryCatalog.ResolveAll([]));
    }

    [Fact]
    public void Covers_ParentIncludesDescendant()
    {
        Assert.True(EditCategoryCatalog.Covers("profanity", "word_damn"));
        Assert.False(EditCategoryCatalog.Covers("word_damn", "profanity"));
        Assert.True(EditCategoryCatalog.Covers("word_damn", "word_damn"));
    }

    [Fact]
    public void Find_AndLeafIds_WalkNestedWords()
    {
        var profanity = EditCategoryCatalog.Find("profanity");
        Assert.NotNull(profanity);
        Assert.Contains("word_ass", EditCategoryCatalog.LeafIds(profanity));
        Assert.Null(EditCategoryCatalog.Find("nope"));
    }

    [Fact]
    public void IsHonored_RestrictsToEnabledSet()
    {
        var overrides = new UserItemEditOverrides { RestrictToCategories = true };
        overrides.EnabledCategories.Add("graphic");
        Assert.True(overrides.IsHonored(["graphic"]));
        Assert.False(overrides.IsHonored(["profanity"]));
        Assert.False(overrides.IsHonored([]));
        Assert.True(new UserItemEditOverrides().IsHonored(["anything"]));
    }

    [Fact]
    public void CategoriesMarkdown_ListsEveryCatalogId()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "categories.md");
        Assert.True(File.Exists(path), path);
        var markdown = File.ReadAllText(path);
        foreach (var id in EditCategoryCatalog.AllMinorIds)
        {
            Assert.Contains("`" + id + "`", markdown, StringComparison.Ordinal);
        }
    }
}
