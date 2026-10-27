using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore.Http.Binding;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// The refusal of a request body, which only the minimal API package answers: the refusal list itself is
/// <c>src/Shared/ValueObjectRefusals.cs</c>, which the Azure Functions ASP.NET Core package links too.
/// </summary>
internal sealed partial class ValueObjectRefusals
{
    /// <summary>
    /// Adds the value object a request body refused, under its JSON path, with the message the resource holds for its
    /// code, if any, or the serializer's.
    /// </summary>
    /// <param name="json">The exception the serializer threw.</param>
    /// <param name="code">The code it carries.</param>
    /// <remarks>
    /// The message quotes the value object by the name of its type as <c>{0}</c>, never by the JSON path, which holds the
    /// key a dictionary keyed by a value object refused, the refused value itself. A type the registry cannot describe,
    /// such as <c>AnyEntityId</c>, has the key of the code alone and no argument. An exception that names no value object
    /// keeps its message.
    /// </remarks>
    public void AddBody(JsonException json, string code)
    {
        string? message = null;
        if (messages?.ResourceType is { } resource && json is ValueObjectJsonException refusal)
        {
            // Described only when a resource is named, so that an endpoint keeping the English messages asks the registry
            // nothing more than it did.
            message = ValueObjectLocalizedMessages.Find(
                resource,
                services,
                ValueObjectBindingFilter.TryDescribe(refusal.ValueObjectType, out var descriptor) ? descriptor : null,
                code,
                ValueObjectLocalizedMessages.NameOf(refusal.ValueObjectType));
        }

        Add(json.Path ?? string.Empty, message ?? json.Message, code);
    }
}
