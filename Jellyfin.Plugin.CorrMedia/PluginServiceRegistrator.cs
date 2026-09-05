using Jellyfin.Plugin.CorrMedia.Configuration;
using Jellyfin.Plugin.CorrMedia.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CorrMedia
{
    /// <summary>
    /// Registers services for the CorrMedia plugin.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<PluginConfiguration>();
            serviceCollection.AddSingleton<EdlEditStore>();
            serviceCollection.AddHostedService<SkipEdl>();

#if PATCHED_CORE
            serviceCollection.AddSingleton<MediaBrowser.Controller.MediaEncoding.ISessionAudioFilterProvider, SessionAudioFilterProvider>();
            serviceCollection.AddSingleton<MediaBrowser.Controller.MediaEncoding.ISessionMediaEditGraphProvider, SessionMediaEditGraphProvider>();
            serviceCollection.AddSingleton<MediaBrowser.Controller.MediaEncoding.ISessionMuteRangeLoader, SessionMuteRangeLoader>();
            serviceCollection.AddSingleton<MediaBrowser.Controller.MediaEncoding.ISessionEdlDeliveryHint, SessionEdlDeliveryHint>();
#else
            serviceCollection.AddSingleton<SessionAudioFilterProvider>();
            serviceCollection.AddSingleton<SessionMediaEditGraphProvider>();
#endif
        }
    }
}
