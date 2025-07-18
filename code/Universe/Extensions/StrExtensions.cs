// create an extension method to convert a string to lower camel case
using System.Text.RegularExpressions;

namespace Universe.Extensions;

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to lower camel case.
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string ToLowerCamelCase(this string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return str;

        str = Regex.Replace(str, @"_([a-zA-Z])", m => m.Groups[1].Value.ToUpper());

        if (Regex.IsMatch(str, @"^[A-Z0-9]+$"))
            return char.ToLowerInvariant(str[0]) + str[1..];

        return $"{char.ToLowerInvariant(str[0])}{str[1..]}";
    }
}
