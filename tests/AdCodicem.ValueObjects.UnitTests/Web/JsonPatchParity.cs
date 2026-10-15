using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// A value object patched through <c>TryApplyTo</c>, beside the same value read from a body by the Newtonsoft.Json
/// converter, for the samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class JsonPatchParity
{
    /// <summary>The settings MVC reads a document and a body under once <c>AddValueObjectsNewtonsoftJson()</c> is in place.</summary>
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings().AddValueObjects();

    /// <summary>
    /// Checks that the text the converter writes of an instance is read, by every operation and in every shape a member
    /// takes, as a body reads it, a dictionary key from that text as the value object parses it; that a refused token is
    /// refused with the code the converter gives it; and that removing the value object is refused.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">The instance a holder starts with.</param>
    /// <param name="large">The instance a document patches in.</param>
    /// <param name="refused">A token the value object refuses, as JSON.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large, string refused)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        var token = JsonConvert.SerializeObject(large, Settings);
        var expected = JsonConvert.DeserializeObject<TSelf>(token, Settings);
        var text = token.StartsWith('"') ? JsonConvert.DeserializeObject<string>(token, Settings)! : token;
        var segment = text.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        TSelf.TryParse(text, CultureInfo.InvariantCulture, out var key).Should().BeTrue("{0} parses its own text {1}", name, text);

        var (applied, holder) = Apply<TSelf>(
            $$"""
            [{"op":"replace","path":"/value","value":{{token}}},{"op":"add","path":"/maybe","value":{{token}}},
             {"op":"add","path":"/values/-","value":{{token}}},{"op":"add","path":"/byName/x","value":{{token}}},
             {"op":"add","path":"/byKey/{{segment}}","value":1},{"op":"test","path":"/value","value":{{token}}},
             {"op":"copy","from":"/value","path":"/values/-"},{"op":"move","from":"/maybe","path":"/values/0"}]
            """,
            small,
            out var errors);
        applied.Should().BeTrue("{0} is patched in as {1}: {2}", name, token, string.Join(" ", errors));
        holder.Value.Should().Be(expected);
        holder.Maybe.Should().BeNull();
        holder.Values.Should().Equal(expected, expected, expected);
        holder.ByName.Should().Equal(new Dictionary<string, TSelf> { ["x"] = expected });
        holder.ByKey.Should().Equal(new Dictionary<TSelf, int> { [key] = 1 });

        var refusal = FluentActions.Invoking(() => JsonConvert.DeserializeObject<NewtonsoftHolder<TSelf>>($$"""{"value":{{refused}}}""", Settings))
            .Should().Throw<JsonSerializationException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        Apply<TSelf>($$"""[{"op":"replace","path":"/value","value":{{refused}}}]""", small, out errors).Applied.Should().BeFalse();
        errors.Should().Equal((refusal.Message, code));

        Apply<TSelf>("""[{"op":"remove","path":"/value"}]""", small, out errors).Applied.Should().BeFalse();
        errors.Should().Equal(($"The value cannot be removed: a valid {name} is required.", ValueObjectErrorCodes.Required));
    }

    private static (bool Applied, PatchHolder<TSelf> Holder) Apply<TSelf>(string json, TSelf small, out List<(string Message, string? Code)> errors)
        where TSelf : struct
    {
        var document = JsonConvert.DeserializeObject<JsonPatchDocument<PatchHolder<TSelf>>>(json, Settings)!;
        var holder = new PatchHolder<TSelf> { Value = small };
        var found = new List<(string Message, string? Code)>();
        var applied = document.TryApplyTo(holder, (error, code) => found.Add((error.ErrorMessage, code)));
        errors = found;

        return (applied, holder);
    }
}

/// <summary>An object holding a value object in every shape a JSON Patch document reaches.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
public sealed class PatchHolder<TSelf>
    where TSelf : struct
{
    /// <summary>Gets or sets the value object.</summary>
    public TSelf Value { get; set; }

    /// <summary>Gets or sets a value object that may be missing.</summary>
    public TSelf? Maybe { get; set; }

    /// <summary>Gets or sets value objects in a list.</summary>
    public List<TSelf> Values { get; set; } = [];

    /// <summary>Gets or sets value objects by name.</summary>
    public Dictionary<string, TSelf> ByName { get; set; } = [];

    /// <summary>Gets or sets numbers keyed by value objects.</summary>
    public Dictionary<TSelf, int> ByKey { get; set; } = [];
}
