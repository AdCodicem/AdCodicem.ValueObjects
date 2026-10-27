using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.AspNetCore.Http;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Reads a refusal as the hosts answer it: the message and the code a value object refuses text with, and the problem a
/// response lists. The minimal API, MVC and Azure Functions tests compare their answers with it.
/// </summary>
public static class ProblemReading
{
    /// <summary>Gets the message and the code a value object refuses text with, as the binder parses it.</summary>
    public static (string Message, string Code) Refusal<TSelf, TValue>(string text)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse($"'{text}' is refused");

        return (validation.ErrorMessage!, validation.ErrorCode!);
    }

    /// <summary>Gets the problem a request answers, read as the messages and the codes of each member.</summary>
    public static async Task<ReadProblem> ReadProblemAsync(HttpResponseMessage response)
        => ProblemOf((int)response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    /// <summary>Gets the problem a response answers, read as the messages and the codes of each member.</summary>
    public static ReadProblem ProblemOf(int status, string body)
    {
        status.Should().Be(StatusCodes.Status400BadRequest, body);
        var problem = JsonDocument.Parse(body).RootElement;

        return new ReadProblem(
            problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!,
            problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>()!);
    }
}

/// <summary>The messages and the codes a validation problem lists for each member, compared as values.</summary>
/// <param name="Messages">The <c>errors</c> member.</param>
/// <param name="Codes">The <c>errorCodes</c> member.</param>
public sealed record ReadProblem(Dictionary<string, string[]> Messages, Dictionary<string, string> Codes)
{
    /// <inheritdoc />
    public bool Equals(ReadProblem? other)
        => other is not null
           && Messages.Count == other.Messages.Count
           && Messages.All(entry => other.Messages.TryGetValue(entry.Key, out var messages) && messages.SequenceEqual(entry.Value))
           && Codes.Count == other.Codes.Count
           && Codes.All(entry => other.Codes.TryGetValue(entry.Key, out var code) && code == entry.Value);

    /// <inheritdoc />
    public override int GetHashCode() => Codes.Count;

    /// <inheritdoc />
    public override string ToString()
        => string.Join("; ", Messages.Select(entry => $"{entry.Key}: [{string.Join(" | ", entry.Value)}] {Codes.GetValueOrDefault(entry.Key)}"));
}

/// <summary>A serializer context that knows the validation problem, and not the <see cref="JsonElement"/> of its codes.</summary>
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class ProblemOnlyJsonContext : JsonSerializerContext;

/// <summary>A serializer context that knows the validation problem and the <see cref="JsonElement"/> of its codes.</summary>
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class ProblemJsonContext : JsonSerializerContext;
