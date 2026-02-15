namespace LumiRise.Api.Models.Alarms;

public sealed class AlarmResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public IReadOnlyCollection<string> DaysOfWeek { get; init; } = [];

    public string Time { get; init; } = string.Empty;

    public string RampMode { get; init; } = "default";

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
