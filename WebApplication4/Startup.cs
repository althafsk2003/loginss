using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Owin;
using Hangfire;
using Hangfire.SqlServer;

namespace WebApplication4
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Use your Hangfire connection string here
            GlobalConfiguration.Configuration
                .UseSqlServerStorage("HangfireConnection");

            // Expose Hangfire Dashboard at /hangfire
            app.UseHangfireDashboard("/hangfire");


            RecurringJob.AddOrUpdate(
                "PendingEventAlertServiceJob",                           // Unique job id
                () => new WakeupServices.PendingEventAlertService().SendPendingEventAlerts(),
                Cron.Minutely
            );


            // Start Hangfire server
            app.UseHangfireServer();
        }
    }
}
