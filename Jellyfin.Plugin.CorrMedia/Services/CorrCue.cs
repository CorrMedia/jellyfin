namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// One subtitle cue on a timeline in seconds.
/// </summary>
/// <param name="Start">Cue start in seconds.</param>
/// <param name="End">Cue end in seconds.</param>
/// <param name="Text">Cue payload (may contain newlines).</param>
/// <param name="Id">Optional cue id.</param>
/// <param name="Settings">WebVTT cue settings after the timestamp arrow, if any.</param>
internal readonly record struct CorrCue(double Start, double End, string Text, string Id = "", string Settings = "");
