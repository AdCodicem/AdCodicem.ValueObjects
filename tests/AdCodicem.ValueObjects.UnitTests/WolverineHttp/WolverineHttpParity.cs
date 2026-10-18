using System.Globalization;
using AdCodicem.ValueObjects.UnitTests.Web;
using AdCodicem.ValueObjects.Wolverine.Http;
using Microsoft.Extensions.Primitives;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// What a value object becomes through the helpers the code Wolverine generates calls, beside what its own
/// <c>TryParse</c> and a minimal API make of the same text, for the samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class WolverineHttpParity
{
    /// <summary>
    /// Checks that the text the smaller instance is parsed from binds to the value object, to its nullable type and as
    /// each element of an array, as <c>TryParse</c> reads it, with nothing to answer; and that the refused text is answered
    /// with the problem a minimal API answers it with, under the name it binds from, its code recorded on the request.
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
        TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
        parsed.Should().Be(small);

        var accepted = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.Bind<TSelf, TValue>(accepted, "value", text, required: true).Should().Be(small, "{0} binds '{1}'", name, text);
        ValueObjectHttpBinding.BindNullable<TSelf, TValue>(accepted, "value", text).Should().Be(small, "{0}? binds '{1}'", name, text);
        ValueObjectHttpBinding.BindArray<TSelf, TValue>(accepted, "values", new StringValues([text, text])).Should().Equal(small, small);
        (await ValueObjectHttpBinding.TryAnswerRefusalsAsync(accepted)).Should().BeFalse("{0} accepts '{1}'", name, text);

        var (message, code) = MinimalApiProblemDetailsTests.Refusal<TSelf, TValue>(refused);
        var request = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindNullable<TSelf, TValue>(request, "value", refused).Should().BeNull("{0} refuses '{1}'", name, refused);
        ValueObjectHttpBinding.BindArray<TSelf, TValue>(request, "values", new StringValues([text, refused])).Should().HaveCount(2);

        var problem = await request.AnswerAsync();
        problem.Messages.Should().ContainKey("value").WhoseValue.Should().Equal(message);
        problem.Messages.Should().ContainKey("values").WhoseValue.Should().Equal(message);
        problem.Codes.Should().Equal(new Dictionary<string, string> { ["value"] = code, ["values"] = code });
        AspNetCore.ValueObjectProblemDetails.GetErrorCodes(request).Should().Equal(problem.Codes);
    }
}
