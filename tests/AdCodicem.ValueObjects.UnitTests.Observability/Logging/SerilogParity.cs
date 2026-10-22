using System.Globalization;
using Serilog;
using Serilog.Events;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// What a value object is logged as through Serilog, beside what the bare value it carries is logged as, for the
/// samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class SerilogParity
{
    /// <summary>
    /// Checks that the policy logs a value object with <c>@</c> as its underlying value, and leaves it without <c>@</c>
    /// to the text <c>ToString()</c> writes, and that the option logs it as its underlying value either way: the scalar
    /// of the underlying type, written in JSON and rendered in a message as the bare value is.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">An instance.</param>
    /// <remarks>
    /// Serilog captures a bare <see cref="Int128"/> or <see cref="UInt128"/>, which is neither a primitive nor a scalar it
    /// knows, as its text without <c>@</c> and as an empty structure with it. The value object hands Serilog the number
    /// itself, which Serilog's JSON formatters write as its digits in a string, the JSON of the bare value without
    /// <c>@</c>: <c>{Message}</c> renders it unquoted, and <c>{Message:lj}</c>, which writes a value of a type it does not
    /// know as JSON, quoted, where it renders the bare value's text the other way round.
    /// </remarks>
    public static void Check<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        var wide = typeof(TValue) == typeof(Int128) || typeof(TValue) == typeof(UInt128);
        var bare = Logged(SerilogCapture.Bare, value.Value);

        var policy = Logged(SerilogCapture.Policy, value);
        policy.Underlying("X").Should().Be(value.ToString(), "without @, the policy leaves {0} to its text", name);
        policy.Json("X").Should().Be(SerilogCapture.JsonOf(new ScalarValue(value.ToString())));
        policy.Plain.Should().Be(new ScalarValue(value.ToString()).ToString());
        policy.Underlying("D").Should().BeOfType<TValue>().And.Be(value.Value);
        policy.Json("D").Should().Be(wide ? bare.Json("X") : bare.Json("D"), "with @, {0} is logged as its {1}", name, typeof(TValue).Name);
        policy.Destructured.Should().Be(wide ? Digits(value.Value) : bare.Destructured);
        policy.DestructuredLiteral.Should().Be(wide ? bare.Json("X") : bare.DestructuredLiteral);

        var optIn = Logged(SerilogCapture.OptIn, value);
        optIn.Underlying("X").Should().BeOfType<TValue>().And.Be(value.Value);
        optIn.Underlying("D").Should().BeOfType<TValue>().And.Be(value.Value);
        optIn.Json("X").Should().Be(bare.Json("X"), "with the option, {0} is logged without @ as its {1}", name, typeof(TValue).Name);
        optIn.Json("D").Should().Be(wide ? bare.Json("X") : bare.Json("D"));
        optIn.Plain.Should().Be(wide ? Digits(value.Value) : bare.Plain);
        optIn.PlainLiteral.Should().Be(wide ? bare.Json("X") : bare.PlainLiteral);
        optIn.Destructured.Should().Be(wide ? Digits(value.Value) : bare.Destructured);
        optIn.DestructuredLiteral.Should().Be(wide ? bare.Json("X") : bare.DestructuredLiteral);
    }

    private static string Digits<TValue>(TValue value) => ((IFormattable)value!).ToString(null, CultureInfo.InvariantCulture);

    private static Events Logged(Func<LoggerConfiguration> configuration, object? value)
        => new(
            SerilogCapture.Capture(configuration(), "{X}", value),
            SerilogCapture.Capture(configuration(), "{@D}", value));

    /// <summary>A value logged without <c>@</c>, as <c>X</c>, and with it, as <c>D</c>.</summary>
    private sealed record Events(LogEvent PlainEvent, LogEvent DestructuredEvent)
    {
        public string Plain => SerilogCapture.Message(PlainEvent);

        public string PlainLiteral => SerilogCapture.Literal(PlainEvent);

        public string Destructured => SerilogCapture.Message(DestructuredEvent);

        public string DestructuredLiteral => SerilogCapture.Literal(DestructuredEvent);

        public string Json(string name) => SerilogCapture.JsonOf(Of(name), name);

        public object? Underlying(string name) => SerilogCapture.Scalar(Of(name), name);

        private LogEvent Of(string name) => name == "X" ? PlainEvent : DestructuredEvent;
    }
}
