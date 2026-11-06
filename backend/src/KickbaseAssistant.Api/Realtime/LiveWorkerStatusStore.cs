using KickbaseAssistant.Infrastructure.Realtime;

namespace KickbaseAssistant.Api.Realtime;

public class LiveWorkerStatusStore
{
    public LiveWorkerStatus? Current { get; set; }
}
