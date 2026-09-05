using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CorrMedia.Configuration
{
    /// <summary>
    /// Configuration options for the CorrMedia plugin.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class with default values.
        /// </summary>
        public PluginConfiguration()
        {
            // Short interval so mute ranges are refreshed soon after playback starts
            SessionCheckInterval = 50;
            PreferEdlApplied = true;
        }

        /// <summary>
        /// Gets or sets how often to check sessions for EDL skip (and refresh mute store), in milliseconds.
        /// </summary>
        public int SessionCheckInterval { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Edited (<c>*_edl</c>) media source
        /// should sort ahead of Original when a sidecar EDL exists. Default is true.
        /// </summary>
        public bool PreferEdlApplied { get; set; }
    }
}
