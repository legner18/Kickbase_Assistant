namespace KickbaseAssistant.Infrastructure.Realtime;

public record LiveWorkerStatus(DateTimeOffset CheckedAt, DateTimeOffset? LastSuccessAt, DateTimeOffset? PausedUntil);
