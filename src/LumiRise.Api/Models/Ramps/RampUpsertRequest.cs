using System.ComponentModel.DataAnnotations;
using LumiRise.Api.Data.Entities;

namespace LumiRise.Api.Models.Ramps;

public sealed class RampUpsertRequest
{
    [Required]
    [StringLength(100)]
    public string Mode { get; init; } = string.Empty;

    [Range(0, 100)]
    public int StartBrightnessPercent { get; init; } = RampProfileEntity.DefaultStartBrightnessPercent;

    [Range(0, 100)]
    public int TargetBrightnessPercent { get; init; } = RampProfileEntity.DefaultTargetBrightnessPercent;

    [Range(1, int.MaxValue)]
    public int RampDurationSeconds { get; init; } = RampProfileEntity.DefaultRampDurationSeconds;
}
