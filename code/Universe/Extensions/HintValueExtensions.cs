using System.Text.Json;
using Universe.Exception;

namespace Universe.Extensions;

/// <summary>
/// Extension methods for safely converting hint values that may be JsonElement objects from deserialized JSON
/// </summary>
internal static class HintValueExtensions
{
    private const int MaxHintIntValue = 10_000;

    /// <summary>
    /// Converts an object to int, handling both JsonElement (from deserialized JSON) and primitive types.
    /// Values are clamped to [0, 10000] to prevent unreasonable query option settings.
    /// </summary>
    public static int ToInt(this object value)
    {
        try
        {
            int result = value is JsonElement jsonElement
                ? jsonElement.GetInt32()
                : Convert.ToInt32(value);

            return Math.Clamp(result, 0, MaxHintIntValue);
        }
        catch (System.Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException)
        {
            throw new UniverseException($"Hint value '{value}' is not a valid integer.", ex);
        }
    }

    /// <summary>
    /// Converts an object to bool, handling both JsonElement (from deserialized JSON) and primitive types
    /// </summary>
    public static bool ToBool(this object value)
    {
        try
        {
            return value is JsonElement jsonElement
                ? jsonElement.GetBoolean()
                : Convert.ToBoolean(value);
        }
        catch (System.Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            throw new UniverseException($"Hint value '{value}' is not a valid boolean.", ex);
        }
    }
}
