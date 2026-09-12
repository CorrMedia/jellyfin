# Sidecar category ids

`edits[].categories` must use ids from this tree. It is the same taxonomy the CorrMedia dashboard shows when a user turns treatments on or off.

This plugin owns the list (`Jellyfin.Plugin.CorrMedia/Models/EditCategoryCatalog.cs`). A later title catalog should reuse these ids, not invent a parallel set.

- Put one or more **ids** on each edit. The `action` (mute, beep, crop, skip, …) is separate and still comes from the sidecar.
- A **parent** id applies the whole group (`profanity` covers every profanity word). A **word** id applies that word only (`word_ass`).
- Prefer the ids below. Dashboard major names (Language, Sex, …) are groupings; most major slugs are not themselves tags.
- Empty or unknown tokens (including `commercial`, `dialogue`) become `other`.
- Some nodes appear under more than one major; the id is the same either place.

## Language (`language`) — hear

### Profanity (`profanity`)

| Id | Dashboard label |
|----|-----------------|
| `word_arse` | *rse |
| `word_ass` | *ss |
| `word_bastard` | b*st*rd |
| `word_bitch` | b*tch |
| `word_bloody` | bl**dy |
| `word_bollocks` | b*ll*cks |
| `word_damn` | d*mn |
| `word_dick` | d*ck |
| `word_douche` | d**che |
| `word_fuck` | f*ck |
| `word_fu` | F.U. |
| `word_goddamn` | g*dd*mn |
| `word_hell` | h*ll |
| `word_piss` | p*ss |
| `word_prick` | pr*ck |
| `word_screw` | scr*w |
| `word_shit` | sh*t |

### Blasphemy (`blasphemy`)

| Id | Dashboard label |
|----|-----------------|
| `word_goddamn` | g*dd*mn (same id as under Profanity) |
| `word_god_derogatory` | God (derogatory) |
| `word_jesus_derogatory` | Jesus (derogatory) |
| `word_christ_derogatory` | Christ (derogatory) |

### Racial & Bigoted Slurs (`racial_slurs`)

| Id | Dashboard label |
|----|-----------------|
| `word_n_word` | n-word |
| `word_other_racial_slurs` | Other racial slurs |
| `word_other_bigoted_slurs` | Other bigoted slurs |

### Childish Language (`childish_language`)

| Id | Dashboard label |
|----|-----------------|
| `word_bs` | BS |
| `word_bum` | bum |
| `word_butt` | butt |
| `word_butthole` | b*tthole |
| `word_crap` | cr*p |
| `word_dumb` | dumb |
| `word_effin` | effin' |
| `word_fart` | fart |
| `word_freakin` | freakin' |
| `word_frickin` | frickin' |
| `word_geez` | geez |
| `word_gosh` | gosh |
| `word_idiot` | idiot |
| `word_omg` | OMG |
| `word_poop` | poop |
| `word_shut_up` | shut up |
| `word_stupid` | stupid |

### Captions with Profanity (`captions_profanity`) — see

| Id | Dashboard label |
|----|-----------------|
| `word_caption_arse` | *rse |
| `word_caption_ass` | *ss |
| `word_caption_bastard` | b*st*rd |
| `word_caption_bitch` | b*tch |
| `word_caption_damn` | d*mn |
| `word_caption_fuck` | f*ck |
| `word_caption_hell` | h*ll |
| `word_caption_shit` | sh*t |
| `word_caption_other` | Other captioned profanity |

### Sexual Reference (`sexual_reference`) — also under Sex

| Id | Dashboard label |
|----|-----------------|
| `word_affair` | affair / cheating |
| `word_hot_sexy` | hot / sexy |
| `word_innuendo` | sexual innuendo |
| `word_porno` | p*rno / p*rnography |
| `word_prostitute` | prostitute |
| `word_slut` | sl*t |
| `word_std` | STD references |
| `word_whore` | wh*re |

## Sex (`sex`) — see

| Id | Dashboard label |
|----|-----------------|
| `sex_with_nudity` | Sex with Nudity (also under Nudity) |
| `sex_without_nudity` | Sex without Nudity |
| `sexual_assault` | Sexual Assault |
| `implied_sex` | Implied Sex |
| `sexually_suggestive` | Sexually Suggestive |
| `sexual_reference` | Sexual Reference (hear; same node as under Language) |
| `vulgar_gestures` | Vulgar Gestures |

## Nudity & Immodesty (`nudity_immodesty`) — see

| Id | Dashboard label |
|----|-----------------|
| `sex_with_nudity` | Sex with Nudity (also under Sex) |
| `female_nudity` | Female Nudity |
| `male_nudity` | Male Nudity |
| `implied_nudity` | Implied Nudity |
| `female_immodesty` | Female Immodesty |
| `male_immodesty` | Male Immodesty |
| `male_female_immodesty` | Male & Female Immodesty |
| `nude_art` | Nude Statues & Paintings |

## Kissing (`kissing`) — see

| Id | Dashboard label |
|----|-----------------|
| `kissing_heterosexual_normal` | Heterosexual Normal |
| `kissing_heterosexual_passionate` | Heterosexual Passionate |
| `kissing_homosexual_normal` | Homosexual Normal |
| `kissing_homosexual_passionate` | Homosexual Passionate |

## Violence (`violence`) — see

| Id | Dashboard label |
|----|-----------------|
| `gore` | Gore |
| `graphic` | Graphic Violence |
| `non_graphic` | Non-graphic Violence |
| `implied_violence` | Implied Violence |
| `disturbing_images` | Disturbing Images |
| `objectionable_scary` | Objectionable, Disturbing, or Scary |

## Drugs & Alcohol (`drugs_alcohol`) — see

| Id | Dashboard label |
|----|-----------------|
| `drugs_illegal` | Illegal Usage |
| `drugs_legal` | Legal Usage |
| `drugs_implied` | Implied Usage |

## Medical & Body Process (`medical`) — see

| Id | Dashboard label |
|----|-----------------|
| `medical_graphic` | Medical Graphic |
| `medical_procedures` | Medical Procedures |
| `life_events` | Life Events |
| `bodily_functions` | Bodily Functions/Jokes |

## Credits & Extras (`credits`) — see

| Id | Dashboard label |
|----|-----------------|
| `opening_credits` | Opening Credits |
| `closing_credits` | Closing Credits |
| `recap_outtakes` | Episode Recap/Outtakes |

## Other (`other`) — see

| Id | Dashboard label |
|----|-----------------|
| `other` | Other |

## Aliases

These sidecar strings are accepted and rewritten. Prefer the canonical ids above.

| Sidecar token | Becomes |
|---------------|---------|
| `sex` | `sex_with_nudity` |
| `immodesty` | `female_immodesty` |
| `nudity`, `nude` | `female_nudity` |
| `sex_nudity`, `sexnudity`, `sex_nudity_immodesty` | `sex_with_nudity` + `female_nudity` |
| `graphic_violence` | `graphic` |
| `nongraphic` | `non_graphic` |
| `violence` | `gore` + `graphic` + `non_graphic` |
| `profane`, `language`, `swear` | `profanity` |
| `racial`, `slur`, `slurs`, `bigoted` | `racial_slurs` |
| `kiss`, `kissing` | `kissing_heterosexual_normal` |
| `drugs`, `alcohol` | `drugs_illegal` |
| `credits` | `opening_credits` |
| `fuck`, `f_word`, `fword` | `word_fuck` |
| `n_word`, `nword` | `word_n_word` |
| `test` | `other` |

A token that already matches a tree id is used as-is. A bare word that matches a `word_*` leaf (`ass` → `word_ass`) is accepted. Spaces, slashes, hyphens, `&`, and `*` are normalized to `_`.
