using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// Fixed filter taxonomy. Sidecar <c>categories</c> map onto these ids.
/// Language groups drill down to specific words; other groups stop at the subcategory.
/// </summary>
public static class EditCategoryCatalog
{
    /// <summary>
    /// Minor id for untagged or unknown sidecar categories.
    /// </summary>
    public const string OtherId = "other";

    /// <summary>
    /// Gets majors in display order.
    /// </summary>
    public static IReadOnlyList<EditCategoryMajor> Majors { get; } =
    [
        new(
            "language",
            "Language",
            EditCategoryChannel.Hear,
            [
                new("profanity", "Profanity", Children: ProfanityWords()),
                new("blasphemy", "Blasphemy", Children: BlasphemyWords()),
                new("racial_slurs", "Racial & Bigoted Slurs", Children: RacialSlurWords()),
                new("childish_language", "Childish Language", Children: ChildishWords()),
                new("captions_profanity", "Captions with Profanity", EditCategoryChannel.See, Children: CaptionProfanityWords()),
                new("sexual_reference", "Sexual Reference", Children: SexualReferenceWords())
            ]),
        new(
            "sex",
            "Sex",
            EditCategoryChannel.See,
            [
                new("sex_with_nudity", "Sex with Nudity"),
                new("sex_without_nudity", "Sex without Nudity"),
                new("sexual_assault", "Sexual Assault"),
                new("implied_sex", "Implied Sex"),
                new("sexually_suggestive", "Sexually Suggestive"),
                new("sexual_reference", "Sexual Reference", EditCategoryChannel.Hear, "Language", SexualReferenceWords()),
                new("vulgar_gestures", "Vulgar Gestures")
            ]),
        new(
            "nudity_immodesty",
            "Nudity & Immodesty",
            EditCategoryChannel.See,
            [
                new("sex_with_nudity", "Sex with Nudity", SharedWith: "Sex"),
                new("female_nudity", "Female Nudity"),
                new("male_nudity", "Male Nudity"),
                new("implied_nudity", "Implied Nudity"),
                new("female_immodesty", "Female Immodesty"),
                new("male_immodesty", "Male Immodesty"),
                new("male_female_immodesty", "Male & Female Immodesty"),
                new("nude_art", "Nude Statues & Paintings")
            ]),
        new(
            "kissing",
            "Kissing",
            EditCategoryChannel.See,
            [
                new("kissing_heterosexual_normal", "Heterosexual Normal"),
                new("kissing_heterosexual_passionate", "Heterosexual Passionate"),
                new("kissing_homosexual_normal", "Homosexual Normal"),
                new("kissing_homosexual_passionate", "Homosexual Passionate")
            ]),
        new(
            "violence",
            "Violence",
            EditCategoryChannel.See,
            [
                new("gore", "Gore"),
                new("graphic", "Graphic Violence"),
                new("non_graphic", "Non-graphic Violence"),
                new("implied_violence", "Implied Violence"),
                new("disturbing_images", "Disturbing Images"),
                new("objectionable_scary", "Objectionable, Disturbing, or Scary")
            ]),
        new(
            "drugs_alcohol",
            "Drugs & Alcohol",
            EditCategoryChannel.See,
            [
                new("drugs_illegal", "Illegal Usage"),
                new("drugs_legal", "Legal Usage"),
                new("drugs_implied", "Implied Usage")
            ]),
        new(
            "medical",
            "Medical & Body Process",
            EditCategoryChannel.See,
            [
                new("medical_graphic", "Medical Graphic"),
                new("medical_procedures", "Medical Procedures"),
                new("life_events", "Life Events"),
                new("bodily_functions", "Bodily Functions/Jokes")
            ]),
        new(
            "credits",
            "Credits & Extras",
            EditCategoryChannel.See,
            [
                new("opening_credits", "Opening Credits"),
                new("closing_credits", "Closing Credits"),
                new("recap_outtakes", "Episode Recap/Outtakes")
            ]),
        new(
            "other",
            "Other",
            EditCategoryChannel.See,
            [
                new(OtherId, "Other")
            ])
    ];

    /// <summary>
    /// Gets every distinct node id in the catalog (parents and leaves).
    /// </summary>
    public static IReadOnlyList<string> AllMinorIds { get; } = FlattenMinorIds();

    /// <summary>
    /// Finds a node by id.
    /// </summary>
    /// <param name="id">Node id.</param>
    /// <returns>The node, or null.</returns>
    public static EditCategoryMinor? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var major in Majors)
        {
            foreach (var minor in major.Minors)
            {
                var found = FindIn(minor, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Leaf ids under <paramref name="node"/> (the node itself when it has no children).
    /// </summary>
    /// <param name="node">Category node.</param>
    /// <returns>Leaf ids.</returns>
    public static IReadOnlyList<string> LeafIds(EditCategoryMinor node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Nested.Count == 0)
        {
            return [node.Id];
        }

        var ids = new List<string>();
        foreach (var child in node.Nested)
        {
            ids.AddRange(LeafIds(child));
        }

        return ids;
    }

    /// <summary>
    /// Returns true when <paramref name="enabledId"/> is <paramref name="editId"/> or an ancestor that covers it.
    /// </summary>
    /// <param name="enabledId">An id the user enabled.</param>
    /// <param name="editId">A resolved sidecar category id.</param>
    /// <returns>True when the enabled node covers the edit tag.</returns>
    public static bool Covers(string enabledId, string editId)
    {
        if (string.Equals(enabledId, editId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var node = Find(enabledId);
        return node is not null && Walk(node).Any(n => string.Equals(n.Id, editId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Maps a sidecar category token to one or more category ids.
    /// Unknown tokens become <see cref="OtherId"/>.
    /// </summary>
    /// <param name="raw">Sidecar category string.</param>
    /// <returns>Category ids.</returns>
    public static IReadOnlyList<string> Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [OtherId];
        }

        var key = Normalize(raw);
        if (AllMinorIds.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return [key];
        }

        var asWord = "word_" + key;
        if (AllMinorIds.Contains(asWord, StringComparer.OrdinalIgnoreCase))
        {
            return [asWord];
        }

        return key switch
        {
            "sex" => ["sex_with_nudity"],
            "immodesty" => ["female_immodesty"],
            "nudity" or "nude" => ["female_nudity"],
            "sex_nudity" or "sexnudity" or "sex_nudity_immodesty" => ["sex_with_nudity", "female_nudity"],
            "graphic_violence" => ["graphic"],
            "nongraphic" => ["non_graphic"],
            "violence" => ["gore", "graphic", "non_graphic"],
            "profane" or "language" or "swear" => ["profanity"],
            "racial" or "slur" or "slurs" or "bigoted" => ["racial_slurs"],
            "kiss" or "kissing" => ["kissing_heterosexual_normal"],
            "drugs" or "alcohol" => ["drugs_illegal"],
            "credits" => ["opening_credits"],
            "f_word" or "fword" or "fuck" => ["word_fuck"],
            "n_word" or "nword" => ["word_n_word"],
            "test" => [OtherId],
            _ => [OtherId]
        };
    }

    /// <summary>
    /// Resolves all tokens on an edit. Empty tags become <see cref="OtherId"/>.
    /// </summary>
    /// <param name="categories">Sidecar categories.</param>
    /// <returns>Distinct category ids.</returns>
    public static IReadOnlyList<string> ResolveAll(IReadOnlyList<string>? categories)
    {
        if (categories is null || categories.Count == 0)
        {
            return [OtherId];
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in categories)
        {
            foreach (var id in Resolve(token))
            {
                set.Add(id);
            }
        }

        return set.Count == 0 ? [OtherId] : [.. set];
    }

    private static string Normalize(string raw)
        => raw.Trim().ToLowerInvariant().Replace(' ', '_').Replace('/', '_').Replace('-', '_').Replace('&', '_').Replace('*', '_');

    private static EditCategoryMinor? FindIn(EditCategoryMinor node, string id)
    {
        if (string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Nested)
        {
            var found = FindIn(child, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<EditCategoryMinor> Walk(EditCategoryMinor node)
    {
        yield return node;
        foreach (var child in node.Nested)
        {
            foreach (var nested in Walk(child))
            {
                yield return nested;
            }
        }
    }

    private static List<string> FlattenMinorIds()
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var major in Majors)
        {
            foreach (var minor in major.Minors)
            {
                foreach (var node in Walk(minor))
                {
                    if (seen.Add(node.Id))
                    {
                        ids.Add(node.Id);
                    }
                }
            }
        }

        return ids;
    }

    private static EditCategoryMinor W(string id, string label)
        => new("word_" + id, label);

    private static IReadOnlyList<EditCategoryMinor> ProfanityWords() =>
    [
        W("arse", "*rse"),
        W("ass", "*ss"),
        W("bastard", "b*st*rd"),
        W("bitch", "b*tch"),
        W("bloody", "bl**dy"),
        W("bollocks", "b*ll*cks"),
        W("damn", "d*mn"),
        W("dick", "d*ck"),
        W("douche", "d**che"),
        W("fuck", "f*ck"),
        W("fu", "F.U."),
        W("goddamn", "g*dd*mn"),
        W("hell", "h*ll"),
        W("piss", "p*ss"),
        W("prick", "pr*ck"),
        W("screw", "scr*w"),
        W("shit", "sh*t")
    ];

    private static IReadOnlyList<EditCategoryMinor> BlasphemyWords() =>
    [
        W("goddamn", "g*dd*mn"),
        W("god_derogatory", "God (derogatory)"),
        W("jesus_derogatory", "Jesus (derogatory)"),
        W("christ_derogatory", "Christ (derogatory)")
    ];

    private static IReadOnlyList<EditCategoryMinor> RacialSlurWords() =>
    [
        W("n_word", "n-word"),
        W("other_racial_slurs", "Other racial slurs"),
        W("other_bigoted_slurs", "Other bigoted slurs")
    ];

    private static IReadOnlyList<EditCategoryMinor> ChildishWords() =>
    [
        W("bs", "BS"),
        W("bum", "bum"),
        W("butt", "butt"),
        W("butthole", "b*tthole"),
        W("crap", "cr*p"),
        W("dumb", "dumb"),
        W("effin", "effin'"),
        W("fart", "fart"),
        W("freakin", "freakin'"),
        W("frickin", "frickin'"),
        W("geez", "geez"),
        W("gosh", "gosh"),
        W("idiot", "idiot"),
        W("omg", "OMG"),
        W("poop", "poop"),
        W("shut_up", "shut up"),
        W("stupid", "stupid")
    ];

    private static IReadOnlyList<EditCategoryMinor> CaptionProfanityWords() =>
    [
        W("caption_arse", "*rse"),
        W("caption_ass", "*ss"),
        W("caption_bastard", "b*st*rd"),
        W("caption_bitch", "b*tch"),
        W("caption_damn", "d*mn"),
        W("caption_fuck", "f*ck"),
        W("caption_hell", "h*ll"),
        W("caption_shit", "sh*t"),
        W("caption_other", "Other captioned profanity")
    ];

    private static IReadOnlyList<EditCategoryMinor> SexualReferenceWords() =>
    [
        W("affair", "affair / cheating"),
        W("hot_sexy", "hot / sexy"),
        W("innuendo", "sexual innuendo"),
        W("porno", "p*rno / p*rnography"),
        W("prostitute", "prostitute"),
        W("slut", "sl*t"),
        W("std", "STD references"),
        W("whore", "wh*re")
    ];
}
