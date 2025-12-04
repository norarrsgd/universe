using System.Text.Json;

namespace Universe.Extensions;

/// <summary>
/// Extension methods for safely converting hint values that may be JsonElement objects from deserialized JSON
/// </summary>
internal static class HintValueExtensions
{
	/// <summary>
	/// Converts an object to int, handling both JsonElement (from deserialized JSON) and primitive types
	/// </summary>
	public static int ToInt(this object value)
	{
		if (value is JsonElement jsonElement)
			return jsonElement.GetInt32();

		return Convert.ToInt32(value);
	}

	/// <summary>
	/// Converts an object to bool, handling both JsonElement (from deserialized JSON) and primitive types
	/// </summary>
	public static bool ToBool(this object value)
	{
		if (value is JsonElement jsonElement)
			return jsonElement.GetBoolean();

		return Convert.ToBoolean(value);
	}
}
