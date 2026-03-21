using Universe.Builder;

namespace Universe.Extensions;

/// <summary>
/// Extension methods for creating fluent query builders on Galaxy instances.
/// </summary>
public static class OrbitExtensions
{
    /// <summary>Create a fluent query builder for this Galaxy repository.</summary>
    public static Orbit<T> Query<T>(this IGalaxy<T> galaxy) where T : class, ICosmicEntity
        => new(galaxy);
}
