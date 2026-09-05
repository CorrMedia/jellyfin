#nullable enable

using System;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionEdlDeliveryHint"/>.
/// </summary>
public sealed class NoOpSessionEdlDeliveryHint : ISessionEdlDeliveryHint
{
    /// <inheritdoc />
    public bool IsEdlAppliedMediaSource(string? mediaSourceId, string? itemId = null) => false;

    /// <inheritdoc />
    public bool TryGetLibraryItemId(string? mediaSourceId, out Guid itemId)
    {
        itemId = default;
        return false;
    }

    /// <inheritdoc />
    public bool RequiresHls(string? itemId, string? mediaSourceId) => false;

    /// <inheritdoc />
    public bool PreferEdlAppliedMediaSources() => false;
}
