using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CorrMedia.Configuration
{
    /// <summary>
    /// Configuration options for the CorrMedia plugin.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the Edited media source
        /// should sort ahead of Original when a sidecar <c>.corr.json</c> exists. Default is true.
        /// </summary>
        public bool PreferEdlApplied { get; set; } = true;
    }
}
