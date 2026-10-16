using KickbaseAssistant.Infrastructure;
using KickbaseAssistant.Worker.Jobs;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKickbaseAssistantDatabase(builder.Configuration);
builder.Services.AddKickbaseAssistantKickbaseClient(builder.Configuration);
builder.Services.AddKickbaseAssistantLigaInsider(builder.Configuration);
builder.Services.AddKickbaseAssistantRealtime(builder.Configuration);

builder.Services.AddQuartz(q =>
{
    var healthCheckJobKey = new JobKey(nameof(KickbaseHealthCheckJob));
    q.AddJob<KickbaseHealthCheckJob>(opts => opts.WithIdentity(healthCheckJobKey));
    q.AddTrigger(t => t
        .ForJob(healthCheckJobKey)
        .WithIdentity($"{nameof(KickbaseHealthCheckJob)}-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(15).RepeatForever())
        .StartNow());

    var germanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    var leagueSyncJobKey = new JobKey(nameof(LeagueSyncJob));
    q.AddJob<LeagueSyncJob>(opts => opts.WithIdentity(leagueSyncJobKey));
    q.AddTrigger(t => t
        .ForJob(leagueSyncJobKey)
        .WithIdentity($"{nameof(LeagueSyncJob)}-trigger")
        // Stündlich: neue Ligen zügig entdecken, verlassene Ligen zügig archivieren.
        .WithCronSchedule("0 0 * * * ?", x => x.InTimeZone(germanyTimeZone)));

    var marketValueSnapshotJobKey = new JobKey(nameof(MarketValueSnapshotJob));
    q.AddJob<MarketValueSnapshotJob>(opts => opts.WithIdentity(marketValueSnapshotJobKey));
    q.AddTrigger(t => t
        .ForJob(marketValueSnapshotJobKey)
        .WithIdentity($"{nameof(MarketValueSnapshotJob)}-trigger")
        // Kickbases echtes Marktwert-Update läuft um 22:00 Uhr. Minütlich von 22:01 bis 22:45,
        // bis der Job selbst eine tatsächliche Änderung sieht (siehe MarketValueSnapshotJob) -
        // ein einzelner Lauf um 21:00 würde regelmäßig vor dem Update feuern und einen
        // "0 Änderung"-Snapshot festschreiben, der nie mehr korrigiert wird.
        .WithCronSchedule("0 1-45 22 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var ligaInsiderScrapeJobKey = new JobKey(nameof(LigaInsiderScrapeJob));
    q.AddJob<LigaInsiderScrapeJob>(opts => opts.WithIdentity(ligaInsiderScrapeJobKey));
    q.AddTrigger(t => t
        .ForJob(ligaInsiderScrapeJobKey)
        .WithIdentity($"{nameof(LigaInsiderScrapeJob)}-trigger")
        // Ein paar Mal täglich, wie in der Planung vorgesehen (LigaInsider hat keine API/ToS-
        // Rate-Limit-Angabe, aber "sparsam abfragen" gilt trotzdem): morgens, mittags, abends.
        .WithCronSchedule("0 0 9,13,18 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var sellAlertJobKey = new JobKey(nameof(SellAlertJob));
    q.AddJob<SellAlertJob>(opts => opts.WithIdentity(sellAlertJobKey));
    q.AddTrigger(t => t
        .ForJob(sellAlertJobKey)
        .WithIdentity($"{nameof(SellAlertJob)}-trigger")
        // Nach dem Ende des Marktwert-Snapshot-Fensters (22:45), damit der heutige Wert sicher
        // in der DB steht.
        .WithCronSchedule("0 50 22 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var liveScoreJobKey = new JobKey(nameof(LiveScoreJob));
    q.AddJob<LiveScoreJob>(opts => opts.WithIdentity(liveScoreJobKey));
    q.AddTrigger(t => t
        .ForJob(liveScoreJobKey)
        .WithIdentity($"{nameof(LiveScoreJob)}-trigger")
        // Feuert immer alle 90s, der Job selbst prüft intern das Zeitfenster/"il"-Flag und tut
        // außerhalb davon nichts (siehe LiveScoreJob-Doku).
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(90).RepeatForever())
        .StartNow());

    var transferMarketScanJobKey = new JobKey(nameof(TransferMarketScanJob));
    q.AddJob<TransferMarketScanJob>(opts => opts.WithIdentity(transferMarketScanJobKey));
    q.AddTrigger(t => t
        .ForJob(transferMarketScanJobKey)
        .WithIdentity($"{nameof(TransferMarketScanJob)}-trigger")
        // Alle 2h während wacher Stunden - Marktlisten ändern sich, wenn Leute kaufen/verkaufen,
        // aber nicht sekündlich wie Live-Punkte.
        .WithCronSchedule("0 0 8-22/2 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var leagueAnalysisJobKey = new JobKey(nameof(LeagueAnalysisJob));
    q.AddJob<LeagueAnalysisJob>(opts => opts.WithIdentity(leagueAnalysisJobKey));
    q.AddTrigger(t => t
        .ForJob(leagueAnalysisJobKey)
        .WithIdentity($"{nameof(LeagueAnalysisJob)}-trigger")
        // Einmal täglich - Budget/Gebotsverhalten sind langsam wandernde, strategische Infos,
        // kein Zeitdruck wie bei Live-Punkten. Nach den anderen Abend-Jobs.
        .WithCronSchedule("0 5 23 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var leagueSquadSyncJobKey = new JobKey(nameof(LeagueSquadSyncJob));
    q.AddJob<LeagueSquadSyncJob>(opts => opts.WithIdentity(leagueSquadSyncJobKey));
    q.AddTrigger(t => t
        .ForJob(leagueSquadSyncJobKey)
        .WithIdentity($"{nameof(LeagueSquadSyncJob)}-trigger")
        // Einmal täglich, kurz nach dem eigenen Marktwert-Snapshot - Kader von Mitspielern
        // ändern sich nicht sekündlich, tägliche Sichtbarkeit reicht.
        .WithCronSchedule("0 55 22 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var dailySummaryJobKey = new JobKey(nameof(DailySummaryJob));
    q.AddJob<DailySummaryJob>(opts => opts.WithIdentity(dailySummaryJobKey));
    q.AddTrigger(t => t
        .ForJob(dailySummaryJobKey)
        .WithIdentity($"{nameof(DailySummaryJob)}-trigger")
        // 10 Min nach der Liga-Analyse, damit alle Abend-Jobs (Snapshot/Alarm/Kader-Sync/Analyse)
        // für heute schon fertig sind, bevor zusammengefasst wird.
        .WithCronSchedule("0 15 23 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var playerMatchdayIngestJobKey = new JobKey(nameof(PlayerMatchdayIngestJob));
    q.AddJob<PlayerMatchdayIngestJob>(opts => opts.WithIdentity(playerMatchdayIngestJobKey));
    q.AddTrigger(t => t
        .ForJob(playerMatchdayIngestJobKey)
        .WithIdentity($"{nameof(PlayerMatchdayIngestJob)}-trigger")
        // Ganz am Ende der Abend-Kette - Spieltagspunkte ändern sich nicht untertägig, es reicht,
        // einmal täglich nach fehlenden Spieltagen zu schauen.
        .WithCronSchedule("0 20 23 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var balanceGuardJobKey = new JobKey(nameof(BalanceGuardJob));
    q.AddJob<BalanceGuardJob>(opts => opts.WithIdentity(balanceGuardJobKey));
    q.AddTrigger(t => t
        .ForJob(balanceGuardJobKey)
        .WithIdentity($"{nameof(BalanceGuardJob)}-trigger")
        // Stündlich, versetzt zu den anderen Jobs: die 24h/3h-Alarmfenster vor Spieltagsbeginn
        // brauchen keine höhere Auflösung, aber auch tagsüber unabhängig von der Abend-Kette.
        .WithCronSchedule("0 30 * * * ?", x => x.InTimeZone(germanyTimeZone)));

    var liveMarketJobKey = new JobKey(nameof(LiveMarketJob));
    q.AddJob<LiveMarketJob>(opts => opts.WithIdentity(liveMarketJobKey));
    q.AddTrigger(t => t
        .ForJob(liveMarketJobKey)
        .WithIdentity($"{nameof(LiveMarketJob)}-trigger")
        // Feuert alle 60s wie LiveScoreJob, der Job selbst pausiert nachts (1-7 Uhr) und pusht nur
        // bei echten Änderungen (siehe MarketChangeDetector) - kein Dauerfeuer ohne neue Daten.
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(60).RepeatForever())
        .StartNow());

    var ownSquadSyncJobKey = new JobKey(nameof(OwnSquadSyncJob));
    q.AddJob<OwnSquadSyncJob>(opts => opts.WithIdentity(ownSquadSyncJobKey));
    q.AddTrigger(t => t
        .ForJob(ownSquadSyncJobKey)
        .WithIdentity($"{nameof(OwnSquadSyncJob)}-trigger")
        // Alle 2 Minuten: Käufe/Verkäufe sollen im Verkaufsplaner zeitnah auftauchen, nicht erst
        // mit dem 22-Uhr-Snapshot. Der Job selbst pausiert nachts und im Snapshot-Fenster.
        .WithCronSchedule("0 0/2 * * * ?", x => x.InTimeZone(germanyTimeZone)));

    // Phase 9+: Punkteprognose + Optimierer wird hier mit eigenem Cron-Schedule registriert.
});

builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
