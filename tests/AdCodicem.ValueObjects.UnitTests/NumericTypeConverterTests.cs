using System.ComponentModel;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The type converter of a numeric value object converts from and to every numeric type a value object may wrap, not
/// only its own: its callers hand it whatever number they hold, Newtonsoft.Json a <see cref="long"/> for every JSON
/// integer and a <see cref="double"/> for every real, a numeric control a <see cref="decimal"/>. The conversion is
/// checked, so a number is refused rather than truncated, and one that fits goes through the rules of the type.
/// </summary>
public sealed class NumericTypeConverterTests
{
    public static TheoryData<Type, object, object> NumbersThatFit => new()
    {
        { typeof(PageNumber), 7L, PageNumber.Create(7) },
        { typeof(Quantity), 7, Quantity.Create(7) },
        { typeof(Quantity), 7.0, Quantity.Create(7) },
        { typeof(Quantity), 7.00m, Quantity.Create(7) },
        { typeof(Quantity), 7f, Quantity.Create(7) },
        { typeof(Quantity), (Int128)1000, Quantity.Create(1000) },
        { typeof(FileSize), long.MaxValue, FileSize.Create(long.MaxValue) },
        { typeof(FileSize), 9.2233720368547748E18, FileSize.Create(9_223_372_036_854_774_784) },
        { typeof(Adjustment), (short)-10, Adjustment.Create(-10) },
        { typeof(Fingerprint), ulong.MaxValue, Fingerprint.Create(ulong.MaxValue) },
        { typeof(LedgerBalance), -1e20, LedgerBalance.Create(Int128.Parse("-100000000000000000000", System.Globalization.CultureInfo.InvariantCulture)) },
        { typeof(Amount), 12.5, Amount.Create(12.5m) },
        { typeof(Amount), 1250L, Amount.Create(1250m) },
        { typeof(Latitude), 45.5m, Latitude.Create(45.5) },
        { typeof(Latitude), 0.5f, Latitude.Create(0.5) },
        { typeof(Ratio), 0.1, Ratio.Create(0.1f) },
        { typeof(Luminance), (byte)200, Luminance.Create(200f) },
    };

    public static TheoryData<Type, object, string> NumbersThatDoNotFit => new()
    {
        { typeof(Quantity), 7.5, "short" },
        { typeof(Quantity), -0.5, "short" },
        { typeof(Quantity), 70000L, "short" },
        { typeof(Quantity), double.NaN, "short" },
        { typeof(Quantity), double.PositiveInfinity, "short" },
        { typeof(Score), -1, "byte" },
        { typeof(ByteCount), -1L, "ulong" },
        { typeof(FileSize), 9.2233720368547758E18, "long" },
        { typeof(LedgerBalance), UInt128.MaxValue, "System.Int128" },
        { typeof(Amount), 1e30, "decimal" },
        { typeof(Amount), double.NaN, "decimal" },
        { typeof(Amount), 1e30f, "decimal" },
        { typeof(Amount), float.PositiveInfinity, "decimal" },
        { typeof(Amount), Int128.MaxValue, "decimal" },
        { typeof(Ratio), 1e300, "float" },
        { typeof(Ratio), UInt128.MaxValue, "float" },
    };

    /// <summary>
    /// Newtonsoft.Json without its converter falls back to the type converter, and hands it a <see cref="long"/> for
    /// every JSON integer: a value object over an <see cref="int"/> is then read from a JSON number like the
    /// <see cref="int"/> it replaces, and so is one over any numeric type, from any number that type holds whole.
    /// </summary>
    [Theory]
    [MemberData(nameof(NumbersThatFit))]
    public void A_number_of_any_numeric_type_converts_when_the_underlying_type_holds_it(Type type, object number, object expected)
    {
        var converter = TypeDescriptor.GetConverter(type);

        converter.CanConvertFrom(number.GetType()).Should().BeTrue();
        converter.ConvertFrom(number).Should().Be(expected);
    }

    /// <summary>
    /// A checked cast would read <c>7.5</c> as 7, and a JSON number past the range of the type would wrap or saturate:
    /// either is another value than the one written. Such a number is refused before any rule runs, as text that is no
    /// number is, and the message names the type but not the number.
    /// </summary>
    [Theory]
    [MemberData(nameof(NumbersThatDoNotFit))]
    public void A_number_the_underlying_type_cannot_hold_whole_is_not_parsable_rather_than_truncated(Type type, object number, string underlying)
    {
        var converter = TypeDescriptor.GetConverter(type);

        var refusal = converter.Invoking(c => c.ConvertFrom(number)).Should().Throw<ValueObjectException>().Which;

        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        refusal.Message.Should().Be($"'{type.Name}' rejected the supplied number: The number is not a valid {underlying}.");
        refusal.ValueObjectType.Should().Be(type);
        refusal.AttemptedValue.Should().Be(number);
    }

    /// <summary>
    /// A number that fits is created as any other value is: a <see cref="long"/> of 5000 for a quantity bounded to
    /// 1000 breaks the bound, not the conversion.
    /// </summary>
    [Fact]
    public void A_number_that_fits_goes_through_the_rules_of_the_type()
    {
        var converter = TypeDescriptor.GetConverter(typeof(Quantity));

        var refusal = converter.Invoking(c => c.ConvertFrom(5000L)).Should().Throw<ValueObjectException>().Which;

        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        refusal.AttemptedValue.Should().Be((short)5000, "the rule refused the value the conversion gave");
        TypeDescriptor.GetConverter(typeof(Amount)).ConvertFrom(12.345).Should().Be(Amount.Create(12.34m), "the amount normalizes");
    }

    /// <summary>
    /// A cast keeps fifteen significant digits of a double and seven of a float when it makes a decimal of them, fewer
    /// than either carries. The converter reads the shortest text that gives the number back, so a decimal holds every
    /// digit the number had, as the Newtonsoft.Json converter reads a JSON number Newtonsoft.Json turned into a double.
    /// </summary>
    [Fact]
    public void A_double_or_a_float_converts_to_a_decimal_with_every_digit_it_carries()
    {
        var converter = TypeDescriptor.GetConverter(typeof(Amount));

        converter.ConvertFrom(12345678901234.56).Should().Be(Amount.Create(12345678901234.56m));
        converter.ConvertFrom(16777216f).Should().Be(Amount.Create(16777216m));
        converter.ConvertFrom(123456.79f).Should().Be(Amount.Create(123456.79m));
    }

    /// <summary>
    /// A double or a float holds NaN and the infinities, so they reach the rules of a value object over one, which
    /// decide. A decimal or an integer has none, and they are no number it can hold.
    /// </summary>
    [Fact]
    public void NaN_carries_over_to_a_real_that_holds_it_and_meets_the_rules_there()
    {
        var ratio = TypeDescriptor.GetConverter(typeof(Ratio));
        var latitude = TypeDescriptor.GetConverter(typeof(Latitude));

        ratio.Invoking(c => c.ConvertFrom(double.NaN))
            .Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        latitude.Invoking(c => c.ConvertFrom(float.NegativeInfinity))
            .Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A numeric control bound to a value object reads its value as the number the control holds. Every numeric type
    /// that holds the value whole gets it; one that cannot is refused as a conversion the converter does not perform,
    /// rather than handed a truncated or saturated number.
    /// </summary>
    [Fact]
    public void A_numeric_value_object_converts_to_any_number_that_holds_its_value_whole()
    {
        var quantity = TypeDescriptor.GetConverter(typeof(Quantity));
        var amount = TypeDescriptor.GetConverter(typeof(Amount));
        var seven = Quantity.Create(7);

        quantity.CanConvertTo(typeof(decimal)).Should().BeTrue();
        quantity.ConvertTo(seven, typeof(double)).Should().Be(7.0);
        quantity.ConvertTo(seven, typeof(decimal)).Should().Be(7m);
        quantity.ConvertTo(seven, typeof(byte)).Should().Be((byte)7);
        quantity.ConvertTo(seven, typeof(UInt128)).Should().Be((UInt128)7);
        amount.ConvertTo(Amount.Create(1.5m), typeof(float)).Should().Be(1.5f);
        amount.ConvertTo(Amount.Create(1250m), typeof(int)).Should().Be(1250);

        amount.Invoking(c => c.ConvertTo(Amount.Create(1.5m), typeof(int))).Should().Throw<NotSupportedException>();
        quantity.Invoking(c => c.ConvertTo(Quantity.Create(1000), typeof(sbyte))).Should().Throw<NotSupportedException>();
        TypeDescriptor.GetConverter(typeof(Adjustment)).Invoking(c => c.ConvertTo(Adjustment.Create(-1), typeof(uint)))
            .Should().Throw<NotSupportedException>();
        TypeDescriptor.GetConverter(typeof(Fingerprint)).Invoking(c => c.ConvertTo(Fingerprint.Create(UInt128.MaxValue), typeof(float)))
            .Should().Throw<NotSupportedException>("a float would hold an infinity, which is not the value");
    }

    /// <summary>
    /// A value object over anything but a number takes text and its own type, and no number: a number is no
    /// identifier, no flag and no character.
    /// </summary>
    [Theory]
    [InlineData(typeof(CustomerId))]
    [InlineData(typeof(Consent))]
    [InlineData(typeof(Grade))]
    [InlineData(typeof(Iban))]
    public void A_value_object_over_another_type_converts_no_number(Type type)
    {
        var converter = TypeDescriptor.GetConverter(type);

        converter.CanConvertFrom(typeof(long)).Should().BeFalse();
        converter.CanConvertTo(typeof(long)).Should().BeFalse();
        converter.Invoking(c => c.ConvertFrom(7L)).Should().Throw<NotSupportedException>();
    }
}
