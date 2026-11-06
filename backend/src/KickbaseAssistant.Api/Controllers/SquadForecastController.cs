using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/squad-forecast")]
public class SquadForecastController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
{
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var squad = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && membership.ManagerId == league.OwnUserId)
            .Select(membership => new { membership.PlayerId, membership.Player!.MarketValue })
            .ToListAsync(ct);

        var balanceStatus = await dbContext.LeagueBalanceStatuses.FirstOrDefaultAsync(status => status.LeagueId == leagueId, ct);
        int? updatesUntilKickoff = balanceStatus is null
            ? null
            : MarketValuePredictor.CountUpdatesUntil(DateTimeOffset.UtcNow, balanceStatus.NextKickoffAt, GermanyTimeZone);

        var forecasts = await playerMetricsService.ComputeForecastsAsync(
            squad.Select(player => (player.PlayerId, player.MarketValue)), updatesUntilKickoff, ct);

        return Ok(new
        {
            nextMatchdayNumber = balanceStatus?.NextMatchdayNumber,
            nextKickoffAt = balanceStatus?.NextKickoffAt,
            updatesUntilKickoff,
            dailyTrend = forecasts.Sum(forecast => forecast.LatestChange ?? 0),
            predictedNextChange = forecasts.Sum(forecast => forecast.PredictedNextChange),
            predictedChangeUntilKickoff = updatesUntilKickoff is null
                ? (double?)null
                : forecasts.Sum(forecast => forecast.PredictedChangeUntilKickoff ?? 0),
            players = forecasts.Select(forecast => new
            {
                playerId = forecast.PlayerId,
                latestChange = forecast.LatestChange,
                predictedChangePercent = forecast.PredictedChangePercent,
                predictedNextChange = forecast.PredictedNextChange,
                predictedChangeUntilKickoff = forecast.PredictedChangeUntilKickoff,
            }),
        });
    }
}
