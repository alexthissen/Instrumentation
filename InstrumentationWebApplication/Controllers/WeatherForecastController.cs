using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InstrumentationWebApplication.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly TelemetryClient client;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, TelemetryClient client)
        {
            _logger = logger;
            this.client = client;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            Activity activity = new Activity("Forecast");
            activity.SetStartTime(DateTime.Now);
            activity.AddTag("ChangedBy", "Alex");
//            activity.Start();

            _logger.LogWarning(new EventId(1001, "Some peculiar event"), "Getting weather forecast log demo");
            using (var dependency = client.StartOperation<DependencyTelemetry>(activity))
            {
                //client.GetMetric()
                //client.TrackMetric()
                Trace.TraceWarning("Getting weather forecast trace demo");
                Trace.TraceInformation("Trace information from controller {0}", nameof(WeatherForecastController));

                var rng = new Random();
                var range = Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateTime.Now.AddDays(index),
                    TemperatureC = rng.Next(-20, 55),
                    Summary = Summaries[rng.Next(Summaries.Length)]
                })
                .ToArray();

                client.GetMetric("Temperature").TrackValue(rng.Next(-20, 55));
                client.TrackEvent("ForecastRetrieved");

                // activity.Stop();
                return range;
            }
        }
    }
}
