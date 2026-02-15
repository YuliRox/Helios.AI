using System.ComponentModel.DataAnnotations;

namespace LumiRise.Api.Models.Alarms;

public sealed class AlarmUpsertRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<string> DaysOfWeek { get; init; } = [];

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string Time { get; init; } = "07:00";

    public bool Enabled { get; init; } = true;

    [Required]
    public string RampMode { get; init; } = "default";
}
