using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LeaderboardWebApi.Infrastructure;
using LeaderboardWebAPI.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Read Azure Key Vault client details from mounted secret in Kubernetes
builder.Configuration.AddJsonFile("secrets/appsettings.secrets.json", optional: true);

// Prepare health checks services
IHealthChecksBuilder healthChecks = builder.Services.AddHealthChecks();

// Add configuration provider for Azure Key Vault
if (!String.IsNullOrEmpty(builder.Configuration["KeyVaultName"]))
{
    Uri keyVaultUri = new Uri(builder.Configuration["KeyVaultName"]);
    ClientSecretCredential credential = new (
        builder.Configuration["KeyVaultTenantID"],
        builder.Configuration["KeyVaultClientID"],
        builder.Configuration["KeyVaultClientSecret"]);
        // For managed identities use:
        //   new DefaultAzureCredential()
    var secretClient = new SecretClient(keyVaultUri, credential);
    builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());

    healthChecks.AddAzureKeyVault(keyVaultUri, credential,
        options => {
            options
            .AddSecret("ApplicationInsights--InstrumentationKey")
            .AddKey("RetroKey");
        }, name: "keyvault"
    );
}

// Database 
builder.Services.AddDbContext<LeaderboardContext>(options =>
{
    string connectionString =
        builder.Configuration.GetConnectionString("LeaderboardContext");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorNumbersToAdd: null);
    });
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
healthChecks.AddDbContextCheck<LeaderboardContext>("database", tags: new[] { "ready" });

// Add log providers
builder.Logging.AddApplicationInsights(
    builder.Configuration["ApplicationInsights:InstrumentationKey"],
    options =>
    {
        options.IncludeScopes = true;
        options.TrackExceptionsAsExceptionTelemetry = true;
    });
builder.Logging.AddSeq("http://seq:5341");
builder.Logging.AddSimpleConsole(options => {
    // New since .NET 5
    options.ColorBehavior = LoggerColorBehavior.Disabled;
    options.IncludeScopes = true;
});

builder.Services
    .AddControllers(options => {
        options.RespectBrowserAcceptHeader = true;
        options.ReturnHttpNotAcceptable = true;
        options.FormatterMappings.SetMediaTypeMappingForFormat("json", new MediaTypeHeaderValue("application/json"));
        options.FormatterMappings.SetMediaTypeMappingForFormat("xml", new MediaTypeHeaderValue("application/xml"));
    })
    .AddNewtonsoftJson(setup => {
        setup.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    })
    .AddXmlSerializerFormatters()
    .AddControllersAsServices(); // For resolving controllers as services via DI

// Instrumentation
builder.AddInstrumentation();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
       builder => builder.AllowAnyOrigin()
       .AllowAnyMethod()
       .AllowAnyHeader()
    );
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1.0", new OpenApiInfo { Title = "Retro Videogames Leaderboard WebAPI", Version = "v1.0" });
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Instance = context.HttpContext.Request.Path,
            Status = StatusCodes.Status400BadRequest,
            Type = "https://asp.net/core",
            Detail = "Please refer to the errors property for additional details."
        };
        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json", "application/problem+xml" }
        };
    };
});

WebApplication app = builder.Build();

app.UseCors("CorsPolicy");

// Health and telemetry
app.MapInstrumentation();

if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Logging from inside Program class");

    // ApplicationServices does not exist anymore
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<LeaderboardContext>().Database.EnsureCreated();
    }
    //app.UseStatusCodePages();
    app.UseDeveloperExceptionPage();
    app.UseSwagger(options => {
        options.RouteTemplate = "openapi/{documentName}/openapi.json";
    });
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/openapi/v1.0/openapi.json", "LeaderboardWebAPI v1.0");
        c.RoutePrefix = "openapi";
    });
}

app.UseAuthorization();
app.MapControllers();
app.Run();