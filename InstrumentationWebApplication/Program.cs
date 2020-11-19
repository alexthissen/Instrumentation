using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.TraceListener;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace InstrumentationWebApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    TextWriterTraceListener listener = new TextWriterTraceListener(Console.Out);
                    services.AddSingleton<TextWriterTraceListener>(listener);

                    //Trace.Listeners.Add(listener);
                    Trace.Listeners.Add(new ApplicationInsightsTraceListener());
                })
                .ConfigureLogging((context, builder) =>
                {
                    TraceSource source = new TraceSource("API", SourceLevels.ActivityTracing);
                    var sourceSwitch = new SourceSwitch("sourceSwitch", "Logging Sample");
                    sourceSwitch.Level = SourceLevels.ActivityTracing;
                    source.Switch = sourceSwitch;

                    builder.ClearProviders();
                    builder.AddConfiguration(context.Configuration.GetSection("Logging"));

                    // Log providers
                    builder.AddApplicationInsights(options =>
                    {
                        options.IncludeScopes = true;
                        options.TrackExceptionsAsExceptionTelemetry = true;
                    });

                    builder.AddConsole(options => { 
                        options.Format = ConsoleLoggerFormat.Systemd; 
                        options.Format = ConsoleLoggerFormat.Default; 
                    });
                    builder.AddDebug();
                    builder.AddTraceSource(source.Switch, builder.Services.BuildServiceProvider().GetRequiredService<TextWriterTraceListener>());
                    builder.AddSeq("http://localhost:5341");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
