using AdCodicem.ValueObjects.Metadata;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AdCodicem.ValueObjects.Swashbuckle;

/// <summary>
/// Makes a parameter Swashbuckle describes as text, though it is a value object, refer to the value object's component.
/// </summary>
/// <remarks>
/// <para>
/// A minimal API names a value object as the parameter's type, and its model as the text the parameter is read from,
/// which is what Swashbuckle describes: a bare string, whatever the value object's underlying type and rules. An MVC
/// action names the value object as both, and Swashbuckle refers to its component already. The parameter is described
/// as Swashbuckle describes an MVC action's, from its declared type, and so as it describes an enumeration whose model is
/// text: a reference to the component, or, for an array, an array of references, with the component's description when
/// the parameter has none. A parameter that is no value object, or that Swashbuckle describes from the value object
/// already, is left as it is.
/// </para>
/// <para>
/// A route constraint follows Swashbuckle's rule for a reference: dropped, unless <c>UseAllOfToExtendReferenceSchemas</c>
/// wraps the reference in an <c>allOf</c>, beside which it is kept.
/// </para>
/// </remarks>
internal sealed class ValueObjectParameterFilter : IParameterFilter
{
    /// <inheritdoc />
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        if (parameter is not OpenApiParameter target
            || context.ApiParameterDescription is not { Type: { } declared } description
            || declared == description.ModelMetadata?.ModelType
            || !ValueObjectRegistry.IsValueObject(declared.GetElementType() ?? declared))
        {
            return;
        }

        target.Schema = context.SchemaGenerator.GenerateSchema(
            declared,
            context.SchemaRepository,
            context.PropertyInfo,
            context.ParameterInfo,
            description.RouteInfo);

        if (string.IsNullOrEmpty(target.Description)
            && target.Schema is OpenApiSchemaReference { Reference.Id: { } id }
            && context.SchemaRepository.Schemas.TryGetValue(id, out var component))
        {
            target.Description = component.Description;
        }
    }
}
