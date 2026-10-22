extern alias testingdata;

using System.Globalization;
using System.Text.Json;
using AdCodicem.ValueObjects.AzureFunctions;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.Azure.Functions.Worker.Converters;
using TypeNames = testingdata::AdCodicem.ValueObjects.Shared.TypeNames;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>
/// What a value object becomes through the Azure Functions converter and the problem details middleware, beside what
/// its own <c>TryParse</c> and a minimal API make of the same text, for the samples of
/// <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class FunctionsParity
{
    private static readonly ValueObjectInputConverter Converter = new();

    /// <summary>
    /// Checks that the converter binds the text the smaller instance is parsed from, to the value object and to its
    /// nullable type, and its JSON string literal, which binds as the text it encodes unless the type accepts the quotes;
    /// that the refused text fails with the code and the message <c>TryParse</c> gives, the type named as C# names it;
    /// and that the middleware answers the refused text as a minimal API answers it.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">The instance the text is parsed to.</param>
    /// <param name="text">The text.</param>
    /// <param name="refused">Text the type refuses.</param>
    /// <returns>The check.</returns>
    public static async Task CheckAsync<TSelf, TValue>(TSelf small, string text, string refused)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        (await ConvertAsync(typeof(TSelf), text)).Value.Should().Be(small, "{0} binds '{1}'", name, text);
        (await ConvertAsync(typeof(TSelf?), text)).Value.Should().Be(small, "{0}? binds '{1}'", name, text);

        var literal = JsonSerializer.Serialize(text);
        var expected = TSelf.TryParse(literal, CultureInfo.InvariantCulture, out var quoted) ? quoted : small;
        (await ConvertAsync(typeof(TSelf), literal)).Value.Should().Be(expected, "{0} binds {1}", name, literal);

        var (message, code) = ProblemReading.Refusal<TSelf, TValue>(refused);
        var refusal = await ConvertAsync(typeof(TSelf), refused);
        refusal.Status.Should().Be(ConversionStatus.Failed, "{0} refuses '{1}'", name, refused);
        refusal.Error!.Message.Should().Be($"The value is not a valid {TypeNames.Of(typeof(TSelf))}: {message} ({code})");
        refusal.Error.ShouldCarry(code);

        var request = FunctionsHttp.Request();
        var invocation = new FunctionInvocation(
            name,
            [FunctionInvocation.Parameter<TSelf>("value")],
            new Dictionary<string, object?> { ["value"] = refused },
            request);
        await FunctionsHttp.Middleware().RefuseAsync(invocation);

        var problem = await FunctionsHttp.AnswerAsync(invocation, request);
        problem.Messages.Should().ContainKey("value").WhoseValue.Should().Equal(message);
        problem.Codes.Should().Equal(new Dictionary<string, string> { ["value"] = code });
    }

    private static async Task<ConversionResult> ConvertAsync(Type type, string text)
        => await Converter.ConvertAsync(new TextContext(type, text));
}
