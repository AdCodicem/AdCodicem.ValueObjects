using Microsoft.AspNetCore.OpenApi;

namespace AdCodicem.ValueObjects.OpenApi;

/// <summary>
/// Wires value object support into OpenAPI document generation.
/// </summary>
public static class ValueObjectOpenApiExtensions
{
    /// <summary>
    /// Documents value objects as their underlying types.
    /// </summary>
    /// <param name="options">OpenAPI options, from <c>AddOpenApi</c>.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    public static OpenApiOptions AddValueObjects(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddSchemaTransformer(new ValueObjectSchemaTransformer());

        return options;
    }
}
