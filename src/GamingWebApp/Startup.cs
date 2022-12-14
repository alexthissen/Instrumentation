using GamingWebApp.Proxy;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using Refit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GamingWebApp
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var policyRegistry = services.AddPolicyRegistry();

            // Centrally stored policies
            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(1500));
            policyRegistry.Add("timeout", timeoutPolicy);

            services.Configure<LeaderboardApiOptions>(Configuration.GetSection(nameof(LeaderboardApiOptions)));

            ConfigureTypedClients(services);
            ConfigureSecurity(services);
            ConfigureTelemetry(services);

            services.AddRazorPages();
        }

        private void ConfigureTelemetry(IServiceCollection services)
        {
            services.AddSingleton<ITelemetryInitializer, ServiceNameInitializer>();
            services.AddApplicationInsightsTelemetry(Configuration);
        }

        private void ConfigureTypedClients(IServiceCollection services)
        {
            var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(1500));
            var retry = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .RetryAsync(3, onRetry: (exception, retryCount) => { 
                    Trace.TraceInformation($"Retry #{retryCount}"); 
                });

            services.AddHttpClient("WebAPIs", options =>
            {
                options.BaseAddress = new Uri(Configuration["LeaderboardApiOptions:BaseUrl"]);
                options.Timeout = TimeSpan.FromMilliseconds(15000);
                options.DefaultRequestHeaders.Add("ClientFactory", "Check");
            })
            .AddPolicyHandler(retry.WrapAsync(timeout))
            .AddTypedClient(client => RestService.For<ILeaderboardClient>(client));
        }

        private void ConfigureSecurity(IServiceCollection services)
        {
            services.AddHsts(
                options =>
                {
                    options.MaxAge = TimeSpan.FromDays(100);
                    options.IncludeSubDomains = true;
                    options.Preload = true;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("Logging from Startup:Configure()");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }
    }
}
