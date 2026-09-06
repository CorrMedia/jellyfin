#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionCorrDeliveryHint"/>.
/// </summary>
public sealed class NoOpSessionCorrDeliveryHint : ISessionCorrDeliveryHint
{
    /// <inheritdoc />
    public bool ShouldApplySidecarEdits(string? itemId) => false;

    /// <inheritdoc />
    public bool RequiresHls(string? itemId) => false;

    /// <inheritdoc />
    public bool TryGetEditedRunTimeTicks(string? itemId, out long runTimeTicks)
    {
        runTimeTicks = 0;
        return false;
    }
}
