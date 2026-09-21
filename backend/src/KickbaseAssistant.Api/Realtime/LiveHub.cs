using Microsoft.AspNetCore.SignalR;

namespace KickbaseAssistant.Api.Realtime;

/// <summary>
/// Placeholder for the Live-Punkte push channel (Phase 6). The Worker will later broadcast
/// live point updates to connected clients via this hub instead of clients polling Kickbase.
/// </summary>
public class LiveHub : Hub;
