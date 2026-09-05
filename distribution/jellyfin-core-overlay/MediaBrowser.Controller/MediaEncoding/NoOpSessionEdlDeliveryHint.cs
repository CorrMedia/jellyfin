#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionEdlDeliveryHint"/>.
/// </summary>
public sealed class NoOpSessionEdlDeliveryHint : ISessionEdlDeliveryHint
{
    /// <inheritdoc />
    public bool IsEdlAppliedMediaSource(string? mediaSourceId) => false;

    /// <inheritdoc />
    public bool RequiresHls(string? itemId, string? mediaSourceId) => false;

    /// <inheritdoc />
    public bool PreferEdlAppliedMediaSources() => false;
}
