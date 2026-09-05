#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionEdlDeliveryHint"/>.
/// </summary>
public sealed class NoOpSessionEdlDeliveryHint : ISessionEdlDeliveryHint
{
    /// <inheritdoc />
    public bool RequiresHls(string? itemId) => false;
}
