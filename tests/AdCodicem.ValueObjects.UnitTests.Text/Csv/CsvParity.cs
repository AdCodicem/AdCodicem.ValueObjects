extern alias testingdata;

using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using AdCodicem.ValueObjects.Testing.Data;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using TypeNames = testingdata::AdCodicem.ValueObjects.Shared.TypeNames;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// What a value object becomes in a CSV column, beside what its own rules make of the same text, for the samples of
/// <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class CsvParity
{
    /// <summary>The cultures a file is written and read in through records: the invariant one, and French.</summary>
    private static readonly CultureInfo[] Cultures = [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("fr-FR")];

    /// <summary>Every culture .NET lists whose calendar is not the Gregorian one: the Thai Buddhist, Persian and Um al-Qura among them.</summary>
    private static readonly CultureInfo[] Calendars =
        [.. CultureInfo.GetCultures(CultureTypes.AllCultures).Where(static culture => culture.DateTimeFormat.Calendar is not GregorianCalendar)];

    /// <summary>
    /// Checks, through <c>AddValueObjects()</c>, that the two instances and every value at an edge of the rules, written
    /// as a column of the value object and of its nullable type, read back to themselves in the invariant culture and in
    /// French, a missing nullable one included, and through the converter in every culture whose calendar is not the
    /// Gregorian one; that the refused text, empty text and the text of every value the schema rules out leave a
    /// <see cref="TypeConverterException"/> carrying the code and the message <c>TryParse</c> gives, or
    /// <see cref="ValueObjectErrorCodes.Required"/> for empty text the type refuses; and that the default instance is
    /// written only where its rules accept it.
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
        var sampler = new ValueObjectSampler(new Random(1));
        var converter = new ValueObjectTypeConverter<TSelf, TValue>();
        using var row = new CsvWriter(new StringWriter(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        List<TSelf> accepted = [small, large, .. sampler.Boundaries<TSelf, TValue>().Select(static value => TSelf.Create(value))];
        foreach (var culture in Cultures)
        {
            var configuration = CsvHarness.Configuration(culture);
            var text = CsvHarness.Write(accepted.Select(static (value, line) => new CsvLine<TSelf> { Line = line, Value = value }), configuration);
            CsvHarness.Read<CsvLine<TSelf>>(text, configuration).Select(static read => read.Value)
                .Should().Equal(accepted, "{0} reads back what it writes in '{1}'", name, culture.Name);

            // Empty text is a missing value in a nullable column, which an instance written as empty text cannot be.
            List<TSelf?> optional = [.. accepted.Where(value => Format(converter, row, value, culture).Length > 0).Select(static value => (TSelf?)value), null];
            var optionalText = CsvHarness.Write(optional.Select(static (value, line) => new CsvLine<TSelf?> { Line = line, Value = value }), configuration);
            CsvHarness.Read<CsvLine<TSelf?>>(optionalText, configuration).Select(static read => read.Value)
                .Should().Equal(optional, "{0}? reads back what it writes in '{1}'", name, culture.Name);
        }

        using (var reader = new CsvReader(new StringReader(string.Empty), CultureInfo.InvariantCulture))
        {
            List<string> misread = [];
            foreach (var culture in Calendars)
            {
                var data = Data(culture);
                foreach (var instance in accepted)
                {
                    var text = converter.ConvertToString(instance, row, data)!;
                    if (!Equals(converter.ConvertFromString(text, reader, data), instance))
                    {
                        misread.Add($"[{culture.Name}] '{text}'");
                    }
                }
            }

            misread.Should().BeEmpty("{0} reads back the text it writes in every culture whose calendar is not the Gregorian one", name);
        }

        List<string> texts = [refused, string.Empty];
        texts.AddRange(sampler.RejectedValues<TSelf, TValue>().Select(static rejected => Text(rejected.Value)));
        Refuses<TSelf, TValue>([.. texts.Distinct(StringComparer.Ordinal).Where(static text => !TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out _))]);

        Writes<TSelf, TValue>();
    }

    /// <summary>
    /// Checks that each text, written as a field of its own row, leaves one refusal carrying the code and the message its
    /// rules give, or <see cref="ValueObjectErrorCodes.Required"/> for empty text, through <c>ReadingExceptionOccurred</c>.
    /// </summary>
    private static void Refuses<TSelf, TValue>(List<string> texts)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = TypeNames.Of(typeof(TSelf));
        List<(string? Code, string Message, string? Text)> refusals = [];
        var configuration = CsvHarness.Configuration() with
        {
            ReadingExceptionOccurred = args =>
            {
                var refusal = (TypeConverterException)args.Exception;
                refusals.Add((refusal.Code(), refusal.FirstLine(), refusal.Text));
                return false;
            },
        };

        var file = "Line,Value\n" + string.Concat(texts.Select(static (text, line) => $"{line},{CsvHarness.Field(text)}\n"));
        CsvHarness.Read<CsvLine<TSelf>>(file, configuration).Should().BeEmpty("{0} refuses every row", name);

        refusals.Should().Equal(texts.Select(text =>
        {
            if (text.Length == 0)
            {
                return (ValueObjectErrorCodes.Required, $"An empty field cannot be read as {name}; declare the member as a nullable {name}? instead.", text);
            }

            TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation);
            return ((string?)validation.ErrorCode, string.IsNullOrEmpty(validation.ErrorMessage)
                ? $"The field is not a valid {name}."
                : $"The field is not a valid {name}: {validation.ErrorMessage}", (string?)text);
        }));
    }

    /// <summary>Checks that the default instance is written only where its rules accept it, and then reads back.</summary>
    private static void Writes<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var uninitialized = new CsvLine<TSelf>();
        var validation = TSelf.Validate(uninitialized.Value.Value);
        if (validation.IsValid)
        {
            // An instance nothing created reads back as the one its value creates, which may tell itself apart from it.
            ((object?)CsvHarness.Read<CsvLine<TSelf>>(CsvHarness.Write([uninitialized])).Single().Value.Value)
                .Should().Be(uninitialized.Value.Value, "{0} reads back its default, which its rules accept", typeof(TSelf).Name);
            return;
        }

        var name = TypeNames.Of(typeof(TSelf));
        var refusal = FluentActions.Invoking(() => CsvHarness.Write([uninitialized])).Should().Throw<TypeConverterException>().Which;
        refusal.Code().Should().Be(validation.ErrorCode);
        refusal.FirstLine().Should().Be(string.IsNullOrEmpty(validation.ErrorMessage)
            ? $"The value to write is not a valid {name}."
            : $"The value to write is not a valid {name}: {validation.ErrorMessage}");
    }

    /// <summary>Writes a value object through the converter, in a culture.</summary>
    private static string Format<TSelf, TValue>(ValueObjectTypeConverter<TSelf, TValue> converter, IWriterRow row, TSelf value, CultureInfo culture)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => converter.ConvertToString(value, row, Data(culture))!;

    /// <summary>Map data of no member, whose options carry a culture.</summary>
    private static MemberMapData Data(CultureInfo culture) => new(null) { TypeConverterOptions = { CultureInfo = culture } };

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
