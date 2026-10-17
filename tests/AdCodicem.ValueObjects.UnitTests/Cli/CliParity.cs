using System.CommandLine;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.Testing.Data;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// What a value object becomes on a System.CommandLine command line, beside what its own rules make of the same text, for
/// the samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class CliParity
{
    /// <summary>
    /// Checks, through an option of the value object, of its nullable type and of a list of it, and through an argument,
    /// that the invariant text of the two instances and of every value at an edge of the rules reads as the instance
    /// <c>TryParse</c> gives; that the refused text, empty text the type refuses and the text of every value the schema
    /// rules out leave one parse error each, naming the option and giving the message <c>TryParse</c> gives, with its code
    /// and the type beside it; and that each known value is completed as text that reads back to it.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">One instance.</param>
    /// <param name="large">Another.</param>
    /// <param name="refused">Text the type refuses.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large, string refused)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        var one = new Option<TSelf>("--value");
        var optional = new Option<TSelf?>("--optional");
        var list = new Option<List<TSelf>>("--list") { AllowMultipleArgumentsPerToken = true };
        var argument = new Argument<TSelf?>("value");
        var root = new Command("app") { one, optional, list, argument }.AcceptValueObjects();
        var sampler = new ValueObjectSampler(new Random(1));

        List<TSelf> accepted = [small, large, .. sampler.Boundaries<TSelf, TValue>().Select(static value => TSelf.Create(value))];
        foreach (var instance in accepted)
        {
            var text = Text(instance.Value);
            TSelf.TryParse(text, CultureInfo.InvariantCulture, out var expected, out _).Should().BeTrue("{0} reads '{1}'", name, text);

            Read(root, ["--value", text]).GetValue(one).Should().Be(expected, "{0} reads '{1}' through an option", name, text);
            Read(root, ["--optional", text]).GetValue(optional).Should().Be(expected, "{0}? reads '{1}'", name, text);
            Read(root, ["--", text]).GetValue(argument).Should().Be(expected, "{0} reads '{1}' through an argument", name, text);
        }

        Read(root, ["--list", .. accepted.Select(static instance => Text(instance.Value))]).GetValue(list)
            .Should().Equal(accepted, "{0} reads each token of a list", name);

        Refuses<TSelf, TValue>(root, refused);
        Refuses<TSelf, TValue>(root, string.Empty);
        foreach (var (value, _) in sampler.RejectedValues<TSelf, TValue>())
        {
            Refuses<TSelf, TValue>(root, Text(value));
        }

        var labels = root.Parse("--value ").GetCompletions("--value ".Length).Select(static item => item.Label).ToList();
        foreach (var known in TSelf.Schema.KnownValues)
        {
            var text = Text((TValue)known);
            labels.Should().Contain(text, "{0} completes its known value", name);
            Read(root, ["--value", text]).GetValue(one).Value.Should().Be((TValue)known, "{0} reads its completion '{1}' back", name, text);
        }
    }

    /// <summary>Checks that text the type refuses leaves one parse error with the message and the code its rules give.</summary>
    private static void Refuses<TSelf, TValue>(Command root, string text)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation))
        {
            return;
        }

        root.Parse(["--value", text]).Refusals().Should().Equal(
            [new CliError($"Option '--value': {validation.ErrorMessage}", validation.ErrorCode, typeof(TSelf))],
            "{0} refuses '{1}'",
            typeof(TSelf).Name,
            text);
    }

    /// <summary>Parses a command line no error refuses.</summary>
    private static ParseResult Read(Command root, string[] args)
    {
        var result = root.Parse(args);
        result.Errors.Select(static error => error.Message).Should().BeEmpty("'{0}' reads", string.Join(' ', args));
        return result;
    }

    /// <summary>
    /// Gets the invariant text of an underlying value, in the form its parser reads and its default format writes: a date
    /// or a time in its round-trip form, a duration in its constant one.
    /// </summary>
    private static string Text<TValue>(TValue value) => value switch
    {
        string text => text,
        TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
        DateOnly or TimeOnly or DateTime or DateTimeOffset => ((IFormattable)value).ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value!.ToString()!,
    };
}
