using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Leagues;
using KickbaseAssistant.Infrastructure.LigaInsider;
using KickbaseAssistant.Infrastructure.Players;
using KickbaseAssistant.Infrastructure.Notifications;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KickbaseAssistant.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database. Used by both Api (reads) and Worker (writes).
    /// </summary>
    public static IServiceCollection AddKickbaseAssistantDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));
        services.AddScoped<PlayerMetricsService>();

        return services;
    }

    /// <summary>
    /// Registers the Kickbase HTTP client. Only the Worker talks to Kickbase directly -
    /// the Api never forwards Kickbase credentials or calls to the frontend.
    /// </summary>
    public static IServiceCollection AddKickbaseAssistantKickbaseClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KickbaseOptions>(configuration.GetSection(KickbaseOptions.SectionName));

        services.AddHttpClient(KickbaseHttpClientNames.Anonymous, (sp, client) =>
        {
            client.BaseAddress = new Uri(
                configuration[$"{KickbaseOptions.SectionName}:BaseUrl"] ?? "https://api.kickbase.com");
        });

        services.AddTransient<KickbaseAuthHandler>();
        services.AddSingleton<KickbaseAuthService>();

        services.AddHttpClient(KickbaseHttpClientNames.Authenticated, (sp, client) =>
        {
            client.BaseAddress = new Uri(
                configuration[$"{KickbaseOptions.SectionName}:BaseUrl"] ?? "https://api.kickbase.com");
        }).AddHttpMessageHandler<KickbaseAuthHandler>();

        services.AddScoped<KickbaseApiClient>();
        services.AddScoped<LeagueSyncService>();

        return services;
    }

    /// <summary>
    /// Registers the LigaInsider scraper client (no API, no auth - see LigaInsiderClient for
    /// the scraping/rate-limit notes) and the ntfy.sh push notification sender used by the
    /// Aufstellungs-Wächter (Phase 4).
    /// </summary>
    public static IServiceCollection AddKickbaseAssistantLigaInsider(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(LigaInsiderHttpClientNames.Default, client =>
        {
            client.BaseAddress = new Uri("https://www.ligainsider.de");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) KickbaseAssistant/1.0 (privat, siehe README)");
        });
        services.AddScoped<LigaInsiderClient>();

        services.Configure<NtfyOptions>(configuration.GetSection(NtfyOptions.SectionName));
        services.AddHttpClient<NtfyPushService>();

        return services;
    }

    /// <summary>
    /// Registers the Worker's SignalR client to the Api's live hub (Phase 6). Only the Worker
    /// uses this - the Api hosts the hub instead (see KickbaseAssistant.Api/Realtime/LiveHub.cs).
    /// </summary>
    public static IServiceCollection AddKickbaseAssistantRealtime(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LiveHubOptions>(configuration.GetSection(LiveHubOptions.SectionName));
        services.AddSingleton<LiveHubClient>();

        return services;
    }
}
