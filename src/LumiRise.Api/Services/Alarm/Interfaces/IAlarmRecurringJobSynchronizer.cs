namespace LumiRise.Api.Services.Alarm.Interfaces;

public interface IAlarmRecurringJobSynchronizer
{
    Task SyncAsync(CancellationToken ct);
}
