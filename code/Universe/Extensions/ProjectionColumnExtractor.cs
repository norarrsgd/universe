using System.Collections.Concurrent;
using System.Reflection;

namespace Universe.Extensions;

/// <summary>
/// Extracts column names from a type's public properties for use with <see cref="Builder.Orbit{T}.Select{TProjection}"/>.
/// Results are cached per type for performance.
/// </summary>
internal static class ProjectionColumnExtractor
{
	private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> Cache = [];

	/// <summary>
	/// Returns the public instance property names for the given type, excluding properties marked with <see cref="JsonIgnoreAttribute"/>.
	/// </summary>
	internal static IReadOnlyList<string> GetColumnNames<T>()
		=> Cache.GetOrAdd(typeof(T), static type =>
			type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
				.Select(p => p.Name)
				.ToList());
}
