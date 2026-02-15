namespace LumiRise.Api.Models.Ramps;

public sealed class RampResponse
{
    public Guid Id { get; init; }

    public string Mode { get; init; } = string.Empty;

    public int StartBrightnessPercent { get; init; }

    public int TargetBrightnessPercent { get; init; }

    public int RampDurationSeconds { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
