using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Compares ISO 639-1 and ISO 639-2 language codes, including bibliographic aliases.
/// </summary>
internal static class SpokenLanguage
{
    private static readonly FrozenDictionary<string, string> BibliographicToTerminology = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["alb"] = "sqi",
        ["arm"] = "hye",
        ["baq"] = "eus",
        ["bur"] = "mya",
        ["chi"] = "zho",
        ["cze"] = "ces",
        ["dut"] = "nld",
        ["fre"] = "fra",
        ["geo"] = "kat",
        ["ger"] = "deu",
        ["gre"] = "ell",
        ["ice"] = "isl",
        ["mac"] = "mkd",
        ["mao"] = "mri",
        ["may"] = "msa",
        ["per"] = "fas",
        ["rum"] = "ron",
        ["slo"] = "slk",
        ["tib"] = "bod",
        ["wel"] = "cym",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> TerminologyToBibliographic = Invert(BibliographicToTerminology);

    private static readonly FrozenDictionary<string, string> TwoToThree = BuildTwoToThree();

    private static readonly FrozenDictionary<string, string> ThreeToTwo = Invert(TwoToThree);

    /// <summary>
    /// Returns true when an edit with <paramref name="editLanguage"/> should play on <paramref name="streamLanguage"/>.
    /// A missing edit language applies to every track. A missing or undetermined stream language rejects tagged edits.
    /// An unrecognized edit code does not apply.
    /// </summary>
    /// <param name="editLanguage">Sidecar language, or null when the edit is not language-specific.</param>
    /// <param name="streamLanguage">Language of the selected audio stream.</param>
    /// <returns>True when the edit applies.</returns>
    public static bool Applies(string? editLanguage, string? streamLanguage)
        => string.IsNullOrWhiteSpace(editLanguage) || Same(editLanguage, streamLanguage);

    /// <summary>
    /// Returns true when both codes name the same language.
    /// </summary>
    /// <param name="left">First code.</param>
    /// <param name="right">Second code.</param>
    /// <returns>True when the codes are equivalent.</returns>
    public static bool Same(string? left, string? right)
    {
        if (!TryGetKeys(left, out var leftKeys) || !TryGetKeys(right, out var rightKeys))
        {
            return false;
        }

        foreach (var key in leftKeys)
        {
            if (rightKeys.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetKeys(string? code, out HashSet<string> keys)
    {
        keys = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var primary = PrimarySubtag(code);
        if (primary.Length is < 2 or > 3 || IsUndetermined(primary))
        {
            return false;
        }

        string terminology;
        if (primary.Length == 2)
        {
            if (!TwoToThree.TryGetValue(primary, out var three))
            {
                return false;
            }

            terminology = three;
            keys.Add(primary);
        }
        else if (BibliographicToTerminology.TryGetValue(primary, out var fromBibliographic))
        {
            terminology = fromBibliographic;
            keys.Add(primary);
        }
        else if (ThreeToTwo.ContainsKey(primary) || TerminologyToBibliographic.ContainsKey(primary))
        {
            terminology = primary;
        }
        else
        {
            return false;
        }

        keys.Add(terminology);
        if (ThreeToTwo.TryGetValue(terminology, out var two))
        {
            keys.Add(two);
        }

        if (TerminologyToBibliographic.TryGetValue(terminology, out var bibliographic))
        {
            keys.Add(bibliographic);
        }

        return keys.Count > 0;
    }

    private static string PrimarySubtag(string code)
    {
        var trimmed = code.Trim().ToLowerInvariant();
        var split = trimmed.IndexOfAny(['-', '_']);
        return split < 0 ? trimmed : trimmed[..split];
    }

    private static bool IsUndetermined(string primary)
        => primary is "und" or "mul" or "zxx" or "unknown" or "undetermined";

    private static FrozenDictionary<string, string> BuildTwoToThree()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            var two = culture.TwoLetterISOLanguageName;
            var three = culture.ThreeLetterISOLanguageName;
            if (two.Length != 2 || three.Length != 3 || two.Equals("iv", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            map.TryAdd(two.ToLowerInvariant(), three.ToLowerInvariant());
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, string> Invert(IReadOnlyDictionary<string, string> source)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            map.TryAdd(pair.Value, pair.Key);
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
