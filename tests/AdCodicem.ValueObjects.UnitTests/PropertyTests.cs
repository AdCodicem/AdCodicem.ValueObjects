using FsCheck;
using FsCheck.Fluent;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Property-based coverage of the invariants <c>IValueObject&lt;TSelf, TValue&gt;</c> states in prose:
/// normalization is idempotent, a non-default instance is normalized and valid by construction, and
/// rejection is a return value rather than an exception.
/// </summary>
/// <remarks>
/// <para>
/// <c>ValueObjectContract</c> already checks these against the handful of values a consumer writes down.
/// That catches the cases someone thought of. These run the same laws against generated input -- hundreds
/// of spellings per property per run, including the whitespace, casing and length edges nobody writes an
/// <c>[InlineData]</c> for.
/// </para>
/// <para>
/// Two traps make such a suite look like coverage without being any. A property conditioned on "the value
/// was accepted" passes vacuously when the generator only ever produces junk, so the generators here mix
/// in values each type is meant to accept and every such property asserts on how often it actually
/// reached the accepting branch. And a property over a wide type never lands on the boundary by chance --
/// one draw in 65536 for <see cref="short"/> -- so the quantity generator biases towards the edges of the
/// declared range rather than trusting luck.
/// </para>
/// </remarks>
public class PropertyTests
{
    /// <summary>Throws on the first counterexample, and stays silent when all of them pass.</summary>
    private static readonly Config Laws = Config.QuickThrowOnFailure.WithMaxTest(500).WithQuietOnSuccess(true);

    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Text as a boundary actually hands it over: arbitrary strings, but also null, empty and the
    /// whitespace-only forms a query string or a CSV column produces far more often than chance would.
    /// </summary>
    private static readonly Gen<string?> Junk = Gen.Frequency(
    [
        (1, Gen.Constant<string?>(null)),
        (1, Gen.Elements<string?>(string.Empty, " ", "   ", "\t", "\r\n", "   ")),
        (8, Gen.Select(ArbMap.Default.ArbFor<string>().Generator, text => (string?)text)),
    ]);

    /// <summary>
    /// Structurally valid IBANs: a country code, ISO 7064 MOD-97-10 check digits computed here rather
    /// than taken from the type under test, and a BBAN of a plausible length.
    /// </summary>
    private static readonly Gen<string> ElectronicIbans =
        from country in Gen.Elements("FR", "DE", "BE", "NL", "ES", "IT", "LU", "PT", "GB")
        from length in Gen.Choose(11, 30)
        from bban in Gen.ArrayOf(Gen.Elements(Base36.ToCharArray()), length)
        select country + CheckDigitsFor(country, new string(bban)) + new string(bban);

    /// <summary>The same IBANs respelled the way people write them: grouped, hyphenated, padded, lower case.</summary>
    private static readonly Gen<(string Canonical, string AsWritten)> IbanSpellings =
        from canonical in ElectronicIbans
        from separator in Gen.Elements("", " ", "-", "  ")
        from groupSize in Gen.Choose(2, 5)
        from lower in ArbMap.Default.ArbFor<bool>().Generator
        from padded in ArbMap.Default.ArbFor<bool>().Generator
        select (canonical, Respell(canonical, separator, groupSize, lower, padded));

    private static readonly Gen<string> Emails =
        from local in Gen.ArrayOf(Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.+_-".ToCharArray()))
            .Where(characters => characters.Length is > 0 and <= 40)
        from host in Gen.Elements("example.com", "Example.COM", "mail.example.org", "sub.domain.example.net")
        from padded in ArbMap.Default.ArbFor<bool>().Generator
        select padded ? $"  {new string(local)}@{host}  " : $"{new string(local)}@{host}";

    /// <summary>Country codes as they arrive: the three known ones, their near misses, and casing noise.</summary>
    private static readonly Gen<string> CountryCodeSpellings =
        from code in Gen.Elements("FR", "BE", "LU", "ES", "DE", "fr", "Be", "lu", "F", "FRA", "")
        from padded in ArbMap.Default.ArbFor<bool>().Generator
        select padded ? $" {code} " : code;

    /// <summary>Order references over the characters an order reference is actually made of.</summary>
    private static readonly Gen<string> OrderReferences =
        from length in Gen.Choose(3, 20)
        from characters in Gen.ArrayOf(
            Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-".ToCharArray()),
            length)
        select new string(characters);

    /// <summary>Arbitrary text, for the laws that must hold whatever arrives.</summary>
    private static readonly Arbitrary<string?> AnyText = Arb.From(Junk);

    /// <summary>
    /// Junk mixed with values the types are meant to accept, for the laws that only say something once
    /// something was accepted.
    /// </summary>
    private static readonly Arbitrary<string?> PlausibleText = Arb.From(Gen.Frequency(
    [
        (3, Junk),
        (3, Gen.Select(ElectronicIbans, text => (string?)text)),
        (2, Gen.Select(IbanSpellings, spelling => (string?)spelling.AsWritten)),
        (3, Gen.Select(Emails, text => (string?)text)),
        // Weighted above the others: only 6 of CountryCodeSpellings' 11 elements are known values, so at
        // equal weight the closed-set law's accepted count landed within a standard deviation of its own
        // threshold and flaked on CI (observed: 48 and 49 against a >50 bound, over 500 trials).
        (6, Gen.Select(CountryCodeSpellings, text => (string?)text)),
    ]));

    /// <summary>
    /// Quantities biased towards the edges of the declared 0..1000 range. A uniform <see cref="short"/>
    /// lands on 1000 once in 65536 draws, so a range property fed by one would never test the boundary it
    /// exists to test -- verified by mutation: widening the expected bound to 999 leaves a uniform
    /// generator green.
    /// </summary>
    private static readonly Arbitrary<short> Quantities = Arb.From(Gen.Frequency(
    [
        (3, Gen.Elements<short>(-2, -1, 0, 1, 2, 998, 999, 1000, 1001, 1002, short.MinValue, short.MaxValue)),
        (3, Gen.Select(Gen.Choose(-10, 1010), value => (short)value)),
        (2, ArbMap.Default.ArbFor<short>().Generator),
    ]));

    /// <summary>
    /// Unrounded amounts over a range a monetary value plausibly covers, weighted towards the exact
    /// half-cents where the rounding mode is the only thing that decides the answer. Deliberately not the
    /// whole of <see cref="decimal"/>: <c>Amount</c> pins the scale by adding <c>0.00m</c>, and near
    /// <see cref="decimal.MaxValue"/> that addition overflows -- a property of this sample type's rounding
    /// rule, not of the framework, and not what these laws are about.
    /// </summary>
    private static readonly Arbitrary<decimal> RawAmounts = Arb.From(
        from units in Gen.Choose(0, 1_000_000)
        from cents in Gen.Choose(0, 99)
        from remainder in Gen.Frequency<decimal>(
        [
            (4, Gen.Constant(0.005m)),
            (1, Gen.Constant(0m)),
            (3, Gen.Select(Gen.Choose(0, 9999), fraction => fraction / 1_000_000m)),
        ])
        select units + (cents / 100m) + remainder);

    /// <summary>Accepted amounts, for the laws about how two of them relate.</summary>
    private static readonly Arbitrary<Amount> Amounts = Arb.From(Gen.Select(RawAmounts.Generator, Amount.Create));

    [Fact]
    public void Normalizing_a_normalized_value_changes_nothing()
        => Prop.ForAll(AnyText, text => Prop.ToProperty(
                Settles(Iban.Normalize, text)
                && Settles(EmailAddress.Normalize, text)
                && Settles(CountryCode.Normalize, text)
                && Settles(Ordering.OrderReference.Normalize, text)))
            .Check(Laws);

    [Fact]
    public void Rejection_is_a_return_value_and_never_an_exception()
        => Prop.ForAll(AnyText, text =>
            {
                // A throw anywhere in here fails the property: that is the assertion.
                Iban.TryCreate(text!, out _, out _);
                EmailAddress.TryCreate(text!, out _, out _);
                CountryCode.TryCreate(text!, out _, out _);
                Ordering.OrderReference.TryCreate(text!, out _, out _);
                Iban.TryParse(text, null, out _);
                EmailAddress.TryParse(text, null, out _);
                return true;
            })
            .Check(Laws);

    [Fact]
    public void An_accepted_value_is_a_normalization_fixed_point()
    {
        var accepted = new Counter();

        Prop.ForAll(PlausibleText, text => Prop.ToProperty(
                IsFixedPoint<Iban, string>(text!, Iban.TryCreate, Iban.Normalize, accepted)
                && IsFixedPoint<EmailAddress, string>(text!, EmailAddress.TryCreate, EmailAddress.Normalize, accepted)
                && IsFixedPoint<CountryCode, string>(text!, CountryCode.TryCreate, CountryCode.Normalize, accepted)))
            .Check(Laws);

        accepted.Count.Should().BeGreaterThan(100, "a law about accepted values proves nothing if nothing was accepted");
    }

    [Fact]
    public void TryParse_and_TryCreate_agree_on_text()
    {
        var accepted = new Counter();

        Prop.ForAll(PlausibleText, text =>
            {
                var created = Iban.TryCreate(text!, out var byCreate, out _);
                var parsed = Iban.TryParse(text, null, out var byParse);

                if (created)
                {
                    accepted.Increment();
                }

                return created == parsed && (!created || byCreate == byParse);
            })
            .Check(Laws);

        accepted.Count.Should().BeGreaterThan(50, "the agreement matters most on the values that are accepted");
    }

    [Fact]
    public void Every_spelling_of_an_IBAN_produces_the_same_value()
        => Prop.ForAll(Arb.From(IbanSpellings), spelling =>
            {
                if (!Iban.TryCreate(spelling.AsWritten, out var written, out var validation))
                {
                    // Surface the rule that fired rather than a bare "false".
                    throw new InvalidOperationException(
                        $"'{spelling.AsWritten}' was rejected as {validation.ErrorCode}: {validation.ErrorMessage}");
                }

                var canonical = Iban.Create(spelling.Canonical);

                return written == canonical
                    && written.GetHashCode() == canonical.GetHashCode()
                    && written.Value == spelling.Canonical;
            })
            .Check(Laws);

    [Fact]
    public void An_IBAN_survives_a_round_trip_through_every_format_it_offers()
        => Prop.ForAll(Arb.From(ElectronicIbans), text =>
            {
                var iban = Iban.Create(text);

                // The print form carries spaces, which normalization strips on the way back in.
                return Iban.Parse(iban.ToString()) == iban
                    && Iban.Parse(iban.ToString(Iban.Formats.Print, null)) == iban
                    && Iban.Parse(iban.ToString(Iban.Formats.Electronic, null)) == iban;
            })
            .Check(Laws);

    [Fact]
    public void The_masked_form_of_an_IBAN_keeps_the_country_and_the_last_four_characters()
        => Prop.ForAll(Arb.From(ElectronicIbans), text =>
            {
                var iban = Iban.Create(text);
                var masked = iban.ToString(Iban.Formats.Masked, null);

                return masked.Length == iban.Value.Length
                    && masked.StartsWith(iban.CountryCode, StringComparison.Ordinal)
                    && masked.EndsWith(iban.Value[^4..], StringComparison.Ordinal)
                    && masked[2..^4].All(character => character == '*');
            })
            .Check(Laws);

    [Fact]
    public void A_closed_value_set_accepts_exactly_its_members()
    {
        var accepted = new Counter();

        Prop.ForAll(PlausibleText, text =>
            {
                var isAccepted = CountryCode.TryCreate(text!, out var country, out _);
                var normalized = text is null ? null : CountryCode.Normalize(text);
                var isMember = normalized is not null
                    && CountryCode.KnownValues.Any(known => known.Value == normalized);

                if (isAccepted)
                {
                    accepted.Increment();
                }

                return isAccepted == isMember && (!isAccepted || country.Value == normalized);
            })
            .Check(Laws);

        accepted.Count.Should().BeGreaterThan(50, "the known values have to be reached for this to mean anything");
    }

    [Fact]
    public void A_declared_range_accepts_exactly_the_values_inside_it()
    {
        var accepted = new Counter();
        var rejected = new Counter();

        Prop.ForAll(Quantities, quantity =>
            {
                var isAccepted = Quantity.TryCreate(quantity, out var created, out _);

                (isAccepted ? accepted : rejected).Increment();

                return isAccepted == (quantity is >= 0 and <= 1000) && (!isAccepted || created.Value == quantity);
            })
            .Check(Laws);

        // Both sides of the boundary have to be exercised, or the law only proves one of them.
        accepted.Count.Should().BeGreaterThan(50);
        rejected.Count.Should().BeGreaterThan(50);
    }

    [Fact]
    public void Case_insensitive_equality_agrees_with_the_hash_code()
        => Prop.ForAll(Arb.From(OrderReferences), text =>
            {
                // Ordinal case folding is only unambiguous over ASCII -- U+212A KELVIN SIGN compares equal
                // to 'k' under OrdinalIgnoreCase but does not round-trip through ToUpperInvariant -- and an
                // order reference is an ASCII token, so the generator stays there.
                var lower = Ordering.OrderReference.Create(text.ToLowerInvariant());
                var upper = Ordering.OrderReference.Create(text.ToUpperInvariant());

                return lower == upper
                    && lower.GetHashCode() == upper.GetHashCode()
                    && lower.CompareTo(upper) == 0;
            })
            .Check(Laws);

    [Fact]
    public void Ordering_is_a_total_order_that_agrees_with_equality()
        => Prop.ForAll(Amounts, Amounts, (left, right) =>
            {
                var forwards = Math.Sign(left.CompareTo(right));
                var backwards = Math.Sign(right.CompareTo(left));

                return forwards == -backwards
                    && (forwards == 0) == (left == right)
                    && (forwards < 0) == (left < right)
                    && (forwards > 0) == (left > right);
            })
            .Check(Laws);

    [Fact]
    public void A_normalizing_rule_holds_for_every_accepted_value()
        => Prop.ForAll(RawAmounts, raw =>
            {
                // Amount rounds to the cent, half to even, and pins the scale at two. The law is stated
                // against the value that went in: asserting that an already-rounded amount survives
                // rounding is true of any rounding mode and proves nothing.
                var amount = Amount.Create(raw);

                return amount.Value == decimal.Round(raw, 2, MidpointRounding.ToEven)
                    && amount.ToString().Split('.') is [_, { Length: 2 }];
            })
            .Check(Laws);

    private static bool Settles(Func<string, string> normalize, string? text)
    {
        var once = normalize(text!);

        return string.Equals(normalize(once), once, StringComparison.Ordinal);
    }

    private delegate bool TryCreate<TSelf, TValue>(TValue value, out TSelf created, out ValidationResult validation)
        where TSelf : struct, IValueObject<TSelf, TValue>;

    private static bool IsFixedPoint<TSelf, TValue>(
        TValue value,
        TryCreate<TSelf, TValue> tryCreate,
        Func<TValue, TValue> normalize,
        Counter accepted)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (!tryCreate(value, out var created, out _))
        {
            return true;
        }

        accepted.Increment();

        return EqualityComparer<TValue>.Default.Equals(created.Value, normalize(value))
            && EqualityComparer<TValue>.Default.Equals(normalize(created.Value), created.Value);
    }

    /// <summary>ISO 7064 MOD-97-10, written independently of the implementation it generates input for.</summary>
    private static string CheckDigitsFor(string country, string bban)
    {
        var remainder = 0;
        foreach (var character in bban + country + "00")
        {
            remainder = char.IsAsciiDigit(character)
                ? ((remainder * 10) + (character - '0')) % 97
                : ((remainder * 100) + (character - 'A' + 10)) % 97;
        }

        return (98 - remainder).ToString("D2");
    }

    private static string Respell(string canonical, string separator, int groupSize, bool lower, bool padded)
    {
        var text = new System.Text.StringBuilder();
        for (var i = 0; i < canonical.Length; i++)
        {
            if (i > 0 && i % groupSize == 0)
            {
                text.Append(separator);
            }

            text.Append(canonical[i]);
        }

        var respelled = lower ? text.ToString().ToLowerInvariant() : text.ToString();

        return padded ? $"  {respelled}  " : respelled;
    }

    /// <summary>
    /// How often a property reached its accepting branch. A class rather than a captured <c>int</c>
    /// because the count has to survive being passed into a helper from inside the property lambda.
    /// </summary>
    private sealed class Counter
    {
        public int Count { get; private set; }

        public void Increment() => Count++;
    }
}
