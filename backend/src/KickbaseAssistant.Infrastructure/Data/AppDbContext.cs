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
    public DbSet<League> Leagues => Set<League>();

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
        });

        builder.Entity<MarketValueSnapshot>(s =>
        {
            s.HasIndex(x => new { x.PlayerId, x.Date }).IsUnique();
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

        builder.Entity<League>(l =>
        {
            l.Property(x => x.Id).HasMaxLength(50);
            l.Property(x => x.Name).IsRequired().HasMaxLength(200);
            l.Property(x => x.CompetitionId).IsRequired().HasMaxLength(50);
            l.Property(x => x.OwnUserId).IsRequired().HasMaxLength(50);
        });
    }
}
