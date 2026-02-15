namespace LumiRise.Api.Configuration;

public sealed class AlarmSettingsOptions
{
    public const string SectionName = "AlarmSettings";

    public string TimeZoneId { get; set; } = "UTC";
}
