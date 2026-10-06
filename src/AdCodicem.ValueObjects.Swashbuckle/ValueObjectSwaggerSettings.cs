using System.Text.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AdCodicem.ValueObjects.Swashbuckle;

/// <summary>
/// What an application told <see cref="ValueObjectSwaggerGenExtensions.AddValueObjects(SwaggerGenOptions, JsonSerializerOptions)"/>,
/// handed to the schema filter Swashbuckle builds.
/// </summary>
/// <param name="options">The options the application writes value objects with, or <see langword="null"/> for the
/// minimal API options.</param>
/// <remarks>
/// Swashbuckle builds a filter with <c>ActivatorUtilities</c>, from the container and the arguments its descriptor holds:
/// this one argument, of a type nothing else registers, keeps its choice of constructor deterministic.
/// </remarks>
internal sealed class ValueObjectSwaggerSettings(JsonSerializerOptions? options)
{
    /// <summary>Gets the options the application writes value objects with, if it named them.</summary>
    public JsonSerializerOptions? Options { get; } = options;
}
