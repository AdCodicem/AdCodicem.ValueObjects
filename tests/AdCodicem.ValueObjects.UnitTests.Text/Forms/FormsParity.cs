using System.Globalization;
using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using AdCodicem.ValueObjects.Testing.Data;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// What a value object becomes in a Blazor form, beside what its own rules make of the same text, for the samples of
/// <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class FormsParity
{
    /// <summary>Every culture .NET lists, the Thai Buddhist, Persian and Um al-Qura calendars' among them.</summary>
    private static readonly CultureInfo[] Cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

    /// <summary>
    /// Checks, in a statically rendered form and so in the invariant culture, that the text an input writes for the two
    /// instances and for every value at an edge of the rules reads back to that instance through <c>TryParse</c>, and that
    /// typing it binds that instance, alone and as the nullable type; that the refused text, and the text of every value
    /// the schema rules out, leave the code and the message <c>TryParse</c> gives, or <see cref="ValueObjectErrorCodes.Required"/>
    /// for empty text; that the nullable type takes empty text as <see langword="null"/>; and that, in every culture .NET
    /// lists, as an interactive form reads it, the text an input writes for each of those instances reads back to it.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">One instance.</param>
    /// <param name="large">Another.</param>
    /// <param name="refused">Text the type refuses.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large, string refused)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        using var context = FormsHarness.Context(FormsHarness.Static);
        var name = typeof(TSelf).Name;
        var sampler = new ValueObjectSampler(new Random(1));

        List<TSelf> accepted = [small, large, .. sampler.Boundaries<TSelf, TValue>().Select(static value => TSelf.Create(value))];
        foreach (var instance in accepted)
        {
            var text = context.Form(new Holder<TSelf> { Value = instance }).Text;
            text.Should().NotBeNull();
            TSelf.TryParse(text, CultureInfo.InvariantCulture, out var read, out _).Should().BeTrue("{0} reads back '{1}'", name, text);
            read.Should().Be(instance, "{0} writes '{1}' for {2}", name, text, instance);

            var typed = context.Form(new Holder<TSelf>());
            typed.Type(text!);
            typed.Model.Should().Be(instance, "{0} binds '{1}'", name, text);
            typed.Code.Should().BeNull();
            typed.Messages.Should().BeEmpty();

            if (text!.Length > 0)
            {
                var optional = context.Form(new Holder<TSelf?>());
                optional.Type(text);
                optional.Model.Should().Be(instance, "{0}? binds '{1}'", name, text);
            }
        }

        Refuses<TSelf, TValue>(context, refused);
        foreach (var (value, rule) in sampler.RejectedValues<TSelf, TValue>())
        {
            var text = Text(value);
            if (!TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out _))
            {
                Refuses<TSelf, TValue>(context, text, rule);
            }
        }

        var emptied = context.Form(new Holder<TSelf?> { Value = large });
        emptied.Type(string.Empty);
        emptied.Model.Should().BeNull();
        emptied.Code.Should().BeNull();

        List<string> misread = [];
        foreach (var culture in Cultures)
        {
            foreach (var instance in accepted)
            {
                var text = ValueObjectInputText<TSelf, TValue>.Write(instance, culture);
                if (!ValueObjectInputText<TSelf, TValue>.Read(text, culture, out var read, out var code, out _) || !read.Equals(instance))
                {
                    misread.Add($"[{culture.Name}] '{text}' {(code.Length > 0 ? code : read.ToString())}");
                }
            }
        }

        misread.Should().BeEmpty("{0} reads back the text it writes in every culture", name);
    }

    /// <summary>Checks that text the type refuses leaves the model, the code and the message its rules give.</summary>
    private static void Refuses<TSelf, TValue>(Bunit.BunitContext context, string text, string? rule = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();
        var field = context.Form(new Holder<TSelf>());

        field.Type(text);

        if (text.Length == 0)
        {
            field.Code.Should().Be(ValueObjectErrorCodes.Required);
            field.Messages.Should().Equal("The Value field is required.");
            return;
        }

        field.Code.Should().Be(validation.ErrorCode, "{0} refuses '{1}' ({2})", typeof(TSelf).Name, text, rule);
        field.Messages.Should().Equal(validation.ErrorMessage);
        field.Text.Should().Be(text);
        field.Invalid.Should().BeTrue();
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
