using LeaderboardWebAPI.Infrastructure;
using LeaderboardWebAPI.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LeaderboardWebAPI.Controllers
{
    public class ScoresController : Controller
    {
        private readonly LeaderboardContext context;
        private readonly TelemetryClient client;

        public ScoresController(LeaderboardContext context, TelemetryClient client)
        {
            this.context = context;
            this.client = client;
        }

        [HttpPost("{nickname}/{game}")]
        public async Task PostScore(string nickname, string game, [FromBody] int points)
        {
            Activity activity = new Activity("Forecast");
            activity.SetStartTime(DateTime.Now);
            activity.AddTag("Gamer", nickname);

            using (var dependency = client.StartOperation<DependencyTelemetry>(activity))
            {
                // Lookup gamer based on nickname
                Gamer gamer = await context.Gamers
                  .FirstOrDefaultAsync(g => g.Nickname.ToLower() == nickname.ToLower())
                  .ConfigureAwait(false);

                if (gamer == null) return;

                // Find highest score for game
                var score = await context.Scores
                      .Where(s => s.Game == game && s.Gamer == gamer)
                      .OrderByDescending(s => s.Points)
                      .FirstOrDefaultAsync()
                      .ConfigureAwait(false);

                if (score == null)
                {
                    score = new Score() { Gamer = gamer, Points = points, Game = game };
                    await context.Scores.AddAsync(score);
                }
                else
                {
                    if (score.Points > points) return;
                    score.Points = points;
                }

                client.TrackEvent("NewHighScore");
                client.GetMetric("HighScore").TrackValue(points);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
