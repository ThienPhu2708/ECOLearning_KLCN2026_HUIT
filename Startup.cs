using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ECOLearning.Startup))]

namespace ECOLearning
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Bật SignalR 2 Hub endpoint (/signalr)
            app.MapSignalR();
        }
    }
}

