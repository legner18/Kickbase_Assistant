using KickbaseAssistant.Domain.Players;

namespace KickbaseAssistant.Infrastructure.Notifications;

public record LivePlayerEventChange(LiveMatch Match, string PlayerId, string PlayerName, IReadOnlyList<int> NewEventKeys, int Points);
