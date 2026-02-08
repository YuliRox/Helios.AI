namespace LumiRise.Api.Extensions;

public static class TimeSpanExtensions
{
    /// <summary>
    /// Clamps a <see cref="TimeSpan"/> to the specified inclusive range.
    /// </summary>
    public static TimeSpan Clamp(this TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (min > max)
            throw new ArgumentException($"min ({min}) must not be greater than max ({max}).");

        return value < min ? min
            : value > max ? max
            : value;
    }
}
