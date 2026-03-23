using System.Text.RegularExpressions;

namespace Universe.Extensions;

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    extension(string str)
    {
        /// <summary>
        /// Converts a string to lower camel case.
        /// This is a general-purpose utility. For query builder column names, prefer configuring
        /// a <see cref="System.Text.Json.JsonNamingPolicy"/> on <see cref="Builder.Options.UniverseSerializer"/>
        /// which automatically transforms column names in queries to match serialized document field names.
        /// </summary>
        public string ToLowerCamelCase()
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            string result = Regex.Replace(str, @"_([a-zA-Z])", m => m.Groups[1].Value.ToUpper());

            if (Regex.IsMatch(result, @"^[A-Z0-9]+$"))
                return char.ToLowerInvariant(result[0]) + result[1..];

            return $"{char.ToLowerInvariant(result[0])}{result[1..]}";
        }
    }
}
