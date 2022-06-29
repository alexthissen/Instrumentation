using Azure.Identity;
using HealthChecks.UI.Client;
using LeaderboardWebAPI.Infrastructure;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeaderboardWebAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<LeaderboardContext>(options =>
            {
                string connectionString =
                    Configuration.GetConnectionString("LeaderboardContext");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                });
            });

            ConfigureSecurity(services);
            ConfigureTelemetry(services);
            ConfigureHealth(services);

            services
                .AddControllers(options => {
                    options.RespectBrowserAcceptHeader = true;
                    options.ReturnHttpNotAcceptable = true;
                    options.FormatterMappings.SetMediaTypeMappingForFormat("xml", new MediaTypeHeaderValue("application/xml"));
                    options.FormatterMappings.SetMediaTypeMappingForFormat("json", new MediaTypeHeaderValue("application/json"));
                })
                .AddNewtonsoftJson(setup => {
                    setup.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                })
                .AddXmlSerializerFormatters();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1.0", new OpenApiInfo { Title = "Retro Videogames Leaderboard WebAPI", Version = "v1.0" });
            });
        }

        private void ConfigureHealth(IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<LeaderboardContext>("database", tags: new[] { "ready" } )
                .AddAzureKeyVault(
                    new Uri(Configuration["KeyVaultName"]),
                    new ClientSecretCredential(
                        Configuration["KeyVaultTenantID"],
                        Configuration["KeyVaultClientID"],
                        Configuration["KeyVaultClientSecret"]), 
                    options => { options
                        .AddSecret("ApplicationInsights--InstrumentationKey")
                        .AddKey("RetroKey");
                    }
                );

            // Uncomment next two lines for self-host healthchecks UI
            //services.AddHealthChecksUI()
            //    .AddSqliteStorage($"Data Source=sqlite.db");
        }

        private void ConfigureSecurity(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                   builder => builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                );
            });
        }

        private void ConfigureTelemetry(IServiceCollection services)
        {
            services.AddSingleton<ITelemetryInitializer, ServiceNameInitializer>();
            services.AddApplicationInsightsTelemetry(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, LeaderboardContext context, ILogger<Startup> logger)
        {
            app.UseCors("CorsPolicy");

            if (env.IsDevelopment())
            {
                logger.LogInformation("Logging from inside Startup::Configure class");

                DbInitializer.Initialize(context).Wait();
                app.UseStatusCodePages();
                app.UseDeveloperExceptionPage();
                app.UseSwagger(options => {
                    options.RouteTemplate = "openapi/{documentName}/openapi.json";
                });
                app.UseSwaggerUI(c => {
                    c.SwaggerEndpoint("/openapi/v1.0/openapi.json", "LeaderboardWebAPI v1.0");
                    c.RoutePrefix = "openapi";
                });
            }

            //app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/ping", new HealthCheckOptions() { Predicate = _ => false });
                endpoints.MapHealthChecks("/health/ready",
                    new HealthCheckOptions()
                    {
                        Predicate = reg => reg.Tags.Contains("ready"),
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    })
                    .RequireHost($"*:{Configuration["ManagementPort"]}");

                endpoints.MapHealthChecks("/health/lively",
                    new HealthCheckOptions()
                    {
                        Predicate = _ => true,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                    })
                .RequireHost($"*:{Configuration["ManagementPort"]}");

                // Uncomment next two lines for self-host healthchecks UI
                //endpoints.MapHealthChecksUI();

                endpoints.MapControllers();
            });
        }
    }
}
