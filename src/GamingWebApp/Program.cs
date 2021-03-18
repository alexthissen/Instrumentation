using Microsoft.ApplicationInsights.TraceListener;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GamingWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddApplicationInsights(options =>
                    {
                        options.IncludeScopes = true;
                        options.TrackExceptionsAsExceptionTelemetry = true;
                    });

                    Trace.Listeners.Add(new ApplicationInsightsTraceListener());
                    Trace.Listeners.Add(new ConsoleTraceListener());
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
