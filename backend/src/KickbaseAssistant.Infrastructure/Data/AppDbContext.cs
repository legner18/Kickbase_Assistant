using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<MarketValueSnapshot> MarketValueSnapshots => Set<MarketValueSnapshot>();
    public DbSet<LineupPrediction> LineupPredictions => Set<LineupPrediction>();
    public DbSet<SellAlert> SellAlerts => Set<SellAlert>();
    public DbSet<MarketScanResult> MarketScanResults => Set<MarketScanResult>();
    public DbSet<SquadPlayerScore> SquadPlayerScores => Set<SquadPlayerScore>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueTransfer> LeagueTransfers => Set<LeagueTransfer>();
    public DbSet<LeagueManagerStat> LeagueManagerStats => Set<LeagueManagerStat>();
    public DbSet<SquadMembership> SquadMemberships => Set<SquadMembership>();
    public DbSet<PlayerMatchdayPerformance> PlayerMatchdayPerformances => Set<PlayerMatchdayPerformance>();
    public DbSet<BalanceAlertSent> BalanceAlertsSent => Set<BalanceAlertSent>();
    public DbSet<LeagueBalanceStatus> LeagueBalanceStatuses => Set<LeagueBalanceStatus>();
    public DbSet<TeamFixture> TeamFixtures => Set<TeamFixture>();
    public DbSet<PlayerSaleLock> PlayerSaleLocks => Set<PlayerSaleLock>();
    public DbSet<MarketListingHistory> MarketListingHistories => Set<MarketListingHistory>();
    public DbSet<PlayerMarking> PlayerMarkings => Set<PlayerMarking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Player>(p =>
        {
            p.Property(x => x.Id).HasMaxLength(50);
            p.Property(x => x.Name).IsRequired().HasMaxLength(200);
            p.Property(x => x.TeamId).IsRequired().HasMaxLength(50);
            p.Property(x => x.TeamName).HasMaxLength(200);

            p.HasIndex(x => x.TeamId);
            p.HasIndex(x => x.IsInSquad);

            p.HasMany(x => x.MarketValueSnapshots)
                .WithOne(s => s.Player)
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasMany(x => x.LineupPredictions)
                .WithOne(l => l.Player)
                .HasForeignKey(l => l.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasMany(x => x.SellAlerts)
                .WithOne(a => a.Player)
                .HasForeignKey(a => a.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasMany(x => x.MarketScanResults)
                .WithOne(r => r.Player)
                .HasForeignKey(r => r.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasMany(x => x.SquadPlayerScores)
                .WithOne(r => r.Player)
                .HasForeignKey(r => r.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasMany(x => x.MatchdayPerformances)
                .WithOne(m => m.Player)
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MarketValueSnapshot>(s =>
        {
            s.HasIndex(x => new { x.PlayerId, x.Date }).IsUnique();
        });

        builder.Entity<PlayerMatchdayPerformance>(m =>
        {
            m.Property(x => x.SeasonId).IsRequired().HasMaxLength(20);

            m.HasIndex(x => new { x.PlayerId, x.SeasonId, x.MatchdayNumber }).IsUnique();
        });

        builder.Entity<BalanceAlertSent>(a =>
        {
            a.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            a.Property(x => x.Window).HasConversion<string>().HasMaxLength(20);

            a.HasIndex(x => new { x.LeagueId, x.MatchdayNumber, x.Window }).IsUnique();
        });

        builder.Entity<LeagueBalanceStatus>(s =>
        {
            s.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            s.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);

            s.HasIndex(x => x.LeagueId).IsUnique();
        });

        builder.Entity<TeamFixture>(f =>
        {
            f.Property(x => x.CompetitionId).IsRequired().HasMaxLength(50);
            f.Property(x => x.TeamId).IsRequired().HasMaxLength(50);
            f.Property(x => x.OpponentTeamId).IsRequired().HasMaxLength(50);
            f.Property(x => x.OpponentTeamName).HasMaxLength(200);

            f.HasIndex(x => new { x.CompetitionId, x.TeamId, x.MatchdayNumber }).IsUnique();
        });

        builder.Entity<PlayerSaleLock>(l =>
        {
            l.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            l.Property(x => x.PlayerId).IsRequired().HasMaxLength(50);

            l.HasIndex(x => new { x.LeagueId, x.PlayerId }).IsUnique();
        });

        builder.Entity<MarketListingHistory>(h =>
        {
            h.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            h.Property(x => x.PlayerId).IsRequired().HasMaxLength(50);
            h.Property(x => x.PlayerName).IsRequired().HasMaxLength(200);
            h.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(10);

            h.HasIndex(x => new { x.LeagueId, x.PlayerId, x.Outcome });
        });

        builder.Entity<PlayerMarking>(m =>
        {
            m.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            m.Property(x => x.PlayerId).IsRequired().HasMaxLength(50);
            m.Property(x => x.Color).HasConversion<string>().HasMaxLength(10);
            m.Property(x => x.OwnLineupEstimate).HasConversion<string>().HasMaxLength(20);

            m.HasIndex(x => new { x.LeagueId, x.PlayerId }).IsUnique();
        });

        builder.Entity<LineupPrediction>(l =>
        {
            l.Property(x => x.Source).IsRequired().HasMaxLength(50);
            l.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            l.HasIndex(x => new { x.PlayerId, x.MatchdayNumber });
        });

        builder.Entity<SellAlert>(a =>
        {
            a.Property(x => x.Signal).HasConversion<string>().HasMaxLength(30);
            a.Property(x => x.Reason).IsRequired().HasMaxLength(500);

            a.HasIndex(x => new { x.PlayerId, x.Signal, x.CreatedAt });
            a.HasIndex(x => x.Acknowledged);
        });

        builder.Entity<MarketScanResult>(r =>
        {
            r.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);

            r.HasIndex(x => new { x.LeagueId, x.OverallScore });
        });

        builder.Entity<SquadPlayerScore>(r =>
        {
            r.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);

            r.HasIndex(x => new { x.LeagueId, x.OverallScore });
        });

        builder.Entity<League>(l =>
        {
            l.Property(x => x.Id).HasMaxLength(50);
            l.Property(x => x.Name).IsRequired().HasMaxLength(200);
            l.Property(x => x.CompetitionId).IsRequired().HasMaxLength(50);
            l.Property(x => x.OwnUserId).IsRequired().HasMaxLength(50);
        });

        builder.Entity<LeagueTransfer>(t =>
        {
            t.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            t.Property(x => x.ManagerId).IsRequired().HasMaxLength(50);
            t.Property(x => x.ManagerName).IsRequired().HasMaxLength(200);
            t.Property(x => x.PlayerId).IsRequired().HasMaxLength(50);
            t.Property(x => x.PlayerName).IsRequired().HasMaxLength(200);
            t.Property(x => x.OtherManagerName).HasMaxLength(200);
            t.Property(x => x.Type).HasConversion<string>().HasMaxLength(10);

            t.HasIndex(x => new { x.ManagerId, x.PlayerId, x.Type, x.OccurredAt }).IsUnique();
            t.HasIndex(x => x.LeagueId);
        });

        builder.Entity<LeagueManagerStat>(s =>
        {
            s.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            s.Property(x => x.ManagerId).IsRequired().HasMaxLength(50);
            s.Property(x => x.ManagerName).IsRequired().HasMaxLength(200);

            s.HasIndex(x => new { x.LeagueId, x.ManagerId }).IsUnique();
        });

        builder.Entity<SquadMembership>(m =>
        {
            m.Property(x => x.LeagueId).IsRequired().HasMaxLength(50);
            m.Property(x => x.ManagerId).IsRequired().HasMaxLength(50);
            m.Property(x => x.ManagerName).IsRequired().HasMaxLength(200);
            m.Property(x => x.PlayerId).IsRequired().HasMaxLength(50);

            m.HasIndex(x => new { x.LeagueId, x.ManagerId, x.PlayerId }).IsUnique();

            m.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
