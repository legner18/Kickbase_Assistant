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
    var marketValueSnapshotJobKey = new JobKey(nameof(MarketValueSnapshotJob));
    q.AddJob<MarketValueSnapshotJob>(opts => opts.WithIdentity(marketValueSnapshotJobKey));
    q.AddTrigger(t => t
        .ForJob(marketValueSnapshotJobKey)
        .WithIdentity($"{nameof(MarketValueSnapshotJob)}-trigger")
        // Kickbase aktualisiert Marktwerte üblicherweise abends; 21:00 liegt sicher danach.
        // Explizite Zeitzone, damit der Server-Standort (Deployment läuft auf UTC) egal ist.
        .WithCronSchedule("0 0 21 * * ?", x => x.InTimeZone(germanyTimeZone)));

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
        // 15 Min nach dem Marktwert-Snapshot, damit der heutige Wert schon in der DB steht.
        .WithCronSchedule("0 15 21 * * ?", x => x.InTimeZone(germanyTimeZone)));

    var liveScoreJobKey = new JobKey(nameof(LiveScoreJob));
    q.AddJob<LiveScoreJob>(opts => opts.WithIdentity(liveScoreJobKey));
    q.AddTrigger(t => t
        .ForJob(liveScoreJobKey)
        .WithIdentity($"{nameof(LiveScoreJob)}-trigger")
        // Feuert immer alle 90s, der Job selbst prüft intern das Zeitfenster/"il"-Flag und tut
        // außerhalb davon nichts (siehe LiveScoreJob-Doku).
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(90).RepeatForever())
        .StartNow());

    // Phase 7+: Transfermarkt-Scanner etc. wird hier mit eigenem Cron-Schedule registriert.
});

builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
