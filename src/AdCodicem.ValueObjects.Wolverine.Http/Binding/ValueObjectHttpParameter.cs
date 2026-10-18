using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.Wolverine.Http.Binding;

/// <summary>Where the text of a value object is read.</summary>
internal enum ValueObjectHttpSource
{
    /// <summary>A route value.</summary>
    Route,

    /// <summary>The query string.</summary>
    Query,

    /// <summary>A header.</summary>
    Header,

    /// <summary>A form field.</summary>
    Form,
}

/// <summary>The shape of a value-object parameter.</summary>
internal enum ValueObjectHttpShape
{
    /// <summary>The value object itself, which cannot be null.</summary>
    Single,

    /// <summary>A nullable value object.</summary>
    Nullable,

    /// <summary>An array of value objects.</summary>
    Array,

    /// <summary>A <see cref="List{T}"/> of value objects.</summary>
    List,

    /// <summary>
    /// An <see cref="IList{T}"/>, <see cref="IReadOnlyList{T}"/> or <see cref="IEnumerable{T}"/> of value objects, which
    /// an array implements.
    /// </summary>
    Sequence,
}

/// <summary>
/// A parameter of an endpoint method the package binds: its value object, its shape, where its text is read and under
/// which name.
/// </summary>
/// <param name="ValueObjectType">The value object, <c>TSelf</c>.</param>
/// <param name="ValueType">Its underlying type, <c>TValue</c>.</param>
/// <param name="Shape">The shape of the parameter.</param>
/// <param name="Source">Where its text is read.</param>
/// <param name="Key">The name it is read under, which a refusal is listed under.</param>
/// <param name="Required">Whether an absent value is refused.</param>
internal sealed record ValueObjectHttpParameter(
    Type ValueObjectType,
    Type ValueType,
    ValueObjectHttpShape Shape,
    ValueObjectHttpSource Source,
    string Key,
    bool Required)
{
    private const string InterfacesPreserved =
        "The parameter type of an endpoint method is referenced by that method, and its IValueObject<TSelf, TValue> "
        + "implementation is part of its public contract, which the trimmer keeps with the type.";

    /// <summary>
    /// Describes a parameter the package binds, or declines one: no value object, a shape Wolverine reads from text in no
    /// such form, a body, a service, an <c>[AsParameters]</c> type, a collection in a route value or a header, or a
    /// <c>[FromRoute(Name = …)]</c> naming no route parameter, which Wolverine reports itself.
    /// </summary>
    /// <param name="chain">The endpoint.</param>
    /// <param name="parameter">The parameter of its method, or of one of its middleware methods.</param>
    /// <param name="described">The description, when the package binds the parameter.</param>
    /// <returns>Whether the package binds the parameter.</returns>
    /// <remarks>
    /// The source is chosen in the order of Wolverine's own strategies, so that the package changes how a value is read,
    /// never where: <c>[FromForm]</c>; the route parameter <c>[FromRoute(Name = …)]</c> names, compared ignoring case, or
    /// one of the parameter's name, compared case-sensitively, as
    /// <see cref="HttpChain.FindRouteVariable(ParameterInfo, out JasperFx.CodeGeneration.Model.Variable)"/> compares each;
    /// a header; the query string.
    /// </remarks>
    public static bool TryDescribe(HttpChain chain, ParameterInfo parameter, [NotNullWhen(true)] out ValueObjectHttpParameter? described)
    {
        described = null;
        var (shape, element) = ShapeOf(parameter.ParameterType);
        if (element is null || !TryGetValueType(element, out var valueType) || IsBoundOtherwise(parameter))
        {
            return false;
        }

        var name = parameter.Name!;
        // An absent value is refused unless the parameter can be null, or has a default value, as a minimal API decides; an
        // optional route segment does not make a value object optional, so that the handler never sees a default instance.
        var required = shape == ValueObjectHttpShape.Single && !parameter.HasDefaultValue;
        var attributes = parameter.GetCustomAttributes(inherit: false);
        ValueObjectHttpSource source;
        string key;
        if (attributes.OfType<FromFormAttribute>().FirstOrDefault() is { } form)
        {
            (source, key) = (ValueObjectHttpSource.Form, string.IsNullOrEmpty(form.Name) ? name : form.Name);
        }
        else if (attributes.OfType<FromRouteAttribute>().FirstOrDefault() is { Name.Length: > 0 } route)
        {
            // Wolverine compares a route name an attribute gives without regard to case, and throws for one the template
            // lacks: declined, so that it does.
            if (chain.RoutePattern?.Parameters.FirstOrDefault(part => string.Equals(part.Name, route.Name, StringComparison.OrdinalIgnoreCase)) is not { } part)
            {
                return false;
            }

            (source, key) = (ValueObjectHttpSource.Route, part.Name);
        }
        else if (chain.RoutePattern?.Parameters.Any(part => part.Name == name) == true)
        {
            // Before a header or a query attribute, as Wolverine's route strategy runs before theirs.
            (source, key) = (ValueObjectHttpSource.Route, name);
        }
        else if (attributes.OfType<IFromHeaderMetadata>().FirstOrDefault() is { } header)
        {
            (source, key) = (ValueObjectHttpSource.Header, string.IsNullOrEmpty(header.Name) ? name : header.Name);
        }
        else
        {
            var query = attributes.OfType<FromQueryAttribute>().FirstOrDefault();
            (source, key) = (ValueObjectHttpSource.Query, string.IsNullOrEmpty(query?.Name) ? name : query.Name);
        }

        if (shape is ValueObjectHttpShape.Array or ValueObjectHttpShape.List or ValueObjectHttpShape.Sequence
            && source is ValueObjectHttpSource.Route or ValueObjectHttpSource.Header)
        {
            return false;
        }

        described = new ValueObjectHttpParameter(element, valueType, shape, source, key, required);
        return true;
    }

    /// <summary>
    /// Tells whether a parameter is bound by something other than its text: a body, a service, an <c>[AsParameters]</c>
    /// type, or an attribute of Wolverine's own that binds it, such as <c>[Entity]</c>.
    /// </summary>
    private static bool IsBoundOtherwise(ParameterInfo parameter)
        => parameter.IsDefined(typeof(AsParametersAttribute), inherit: false)
           || parameter.IsDefined(typeof(FromBodyAttribute), inherit: false)
           || parameter.IsDefined(typeof(FromServicesAttribute), inherit: false)
           || parameter.IsDefined(typeof(FromKeyedServicesAttribute), inherit: false)
           || parameter.IsDefined(typeof(NotBodyAttribute), inherit: false)
           || parameter.IsDefined(typeof(WolverineParameterAttribute), inherit: true);

    /// <summary>Finds the shape of a parameter type and the type of its elements, if it is one the package reads.</summary>
    private static (ValueObjectHttpShape Shape, Type? Element) ShapeOf(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return (ValueObjectHttpShape.Nullable, underlying);
        }

        if (type.IsArray)
        {
            return type.GetArrayRank() == 1 ? (ValueObjectHttpShape.Array, type.GetElementType()) : (ValueObjectHttpShape.Array, null);
        }

        if (type.IsGenericType && type.GetGenericArguments() is [var element])
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>))
            {
                return (ValueObjectHttpShape.List, element);
            }

            if (definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(IEnumerable<>))
            {
                return (ValueObjectHttpShape.Sequence, element);
            }
        }

        return (ValueObjectHttpShape.Single, type);
    }

    /// <summary>
    /// Finds the underlying type of a struct implementing <see cref="IValueObject{TSelf, TValue}"/> over itself, the
    /// registry never asked: the generated code names the value object, so one written by hand and a construction of a
    /// generic one need no registration.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern", Justification = InterfacesPreserved)]
    internal static bool TryGetValueType(Type type, [NotNullWhen(true)] out Type? valueType)
    {
        if (type.IsValueType && !type.ContainsGenericParameters && typeof(IValueObject).IsAssignableFrom(type))
        {
            foreach (var candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IValueObject<,>)
                    && candidate.GetGenericArguments()[0] == type)
                {
                    valueType = candidate.GetGenericArguments()[1];
                    return true;
                }
            }
        }

        valueType = null;
        return false;
    }
}
