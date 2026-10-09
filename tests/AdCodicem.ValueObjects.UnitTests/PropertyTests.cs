using AdCodicem.ValueObjects.FsCheck;
using AdCodicem.ValueObjects.Testing.Data;
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
/// The values each type accepts are drawn by the FsCheck package's arbitraries, <c>ValueObjectArbitrary</c>,
/// from the rules the type declares, rather than by generators restating them here; FsCheck draws the seed,
/// so that a failing run replays.
/// The IBAN's MOD-97 check digits are a rule no schema carries, so its generator is registered with the
/// sampler. The deliberately invalid input -- the junk, the near misses -- stays written by hand.
/// </para>
/// <para>
/// Two traps make such a suite look like coverage without being any. A property conditioned on "the value
/// was accepted" passes vacuously when the generator only ever produces junk, so the generators here mix
/// in values each type is meant to accept and every such property asserts on how often it actually
/// reached the accepting branch. And a property over a wide type never lands on the boundary by chance --
/// one draw in 65536 for <see cref="short"/> -- so the quantity generator biases towards the edges the
/// sampler derives from the declared range rather than trusting luck.
/// </para>
/// </remarks>
public class PropertyTests
{
    /// <summary>Throws on the first counterexample, and stays silent when all of them pass.</summary>
    private static readonly Config Laws = Config.QuickThrowOnFailure.WithMaxTest(500).WithQuietOnSuccess(true);

    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// The generators of the rules no schema carries: the check digits of an IBAN.
    /// </summary>
    private static readonly ValueObjectSamplerOptions Sampling = new ValueObjectSamplerOptions().Use<Iban, string>(ElectronicIban);

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
    /// Structurally valid IBANs, drawn by the sampler from the generator registered for them.
    /// </summary>
    private static readonly Gen<string> ElectronicIbans = Sampled<Iban, string>().Select(iban => iban.Value);

    /// <summary>The same IBANs respelled the way people write them: grouped, hyphenated, padded, lower case.</summary>
    private static readonly Gen<(string Canonical, string AsWritten)> IbanSpellings =
        from canonical in ElectronicIbans
        from separator in Gen.Elements("", " ", "-", "  ")
        from groupSize in Gen.Choose(2, 5)
        from lower in ArbMap.Default.ArbFor<bool>().Generator
        from padded in ArbMap.Default.ArbFor<bool>().Generator
        select (canonical, Respell(canonical, separator, groupSize, lower, padded));

    /// <summary>
    /// Addresses the sampler draws from the pattern and the length an address declares, respelled with the noise its
    /// normalizer takes out -- padding and an upper-case domain -- so that the fixed-point law meets spellings that are
    /// not yet normalized.
    /// </summary>
    private static readonly Gen<string> Emails =
        from address in Sampled<EmailAddress, string>()
        from shouting in ArbMap.Default.ArbFor<bool>().Generator
        from padded in ArbMap.Default.ArbFor<bool>().Generator
        let at = address.Value.IndexOf('@', StringComparison.Ordinal)
        let spelled = shouting ? address.Value[..at] + address.Value[at..].ToUpperInvariant() : address.Value
        select padded ? $"  {spelled}  " : spelled;

    /// <summary>
    /// Country codes as they arrive: the known ones, which the sampler draws from the closed set, respelled with casing
    /// noise; their near misses; and padding.
    /// </summary>
    private static readonly Gen<string> CountryCodeSpellings =
        from code in Gen.Frequency(
        [
            // Six to five, the share of known values among the spellings this generator was calibrated on.
            (6, from country in Sampled<CountryCode, string>()
                from casing in Gen.Elements("upper", "lower", "title")
                select casing switch
                {
                    "upper" => country.Value,
                    "lower" => country.Value.ToLowerInvariant(),
                    _ => country.Value[..1] + country.Value[1..].ToLowerInvariant(),
                }),
            (5, Gen.Elements("ES", "DE", "F", "FRA", "")),
        ])
        from padded in ArbMap.Default.ArbFor<bool>().Generator
        select padded ? $" {code} " : code;

    /// <summary>
    /// Order references, which the sampler draws from the lengths the type declares, as ASCII letters and digits since it
    /// declares no pattern.
    /// </summary>
    private static readonly Gen<string> OrderReferences = Sampled<Ordering.OrderReference, string>().Select(reference => reference.Value);

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
        // Weighted above the others: only 6 of CountryCodeSpellings' 11 shares are known values, so at
        // equal weight the closed-set law's accepted count landed within a standard deviation of its own
        // threshold and flaked on CI (observed: 48 and 49 against a >50 bound, over 500 trials).
        (6, Gen.Select(CountryCodeSpellings, text => (string?)text)),
    ]));

    /// <summary>
    /// Quantities biased towards the edges of the declared 0..1000 range: the accepted values at its edges and the values
    /// one step past them, which the sampler derives from the declared bounds, beside values it draws inside them and a
    /// uniform <see cref="short"/>. A uniform <see cref="short"/> alone lands on 1000 once in 65536 draws, so a range
    /// property fed by one would never test the boundary it exists to test -- verified by mutation: widening the expected
    /// bound to 999 leaves a uniform generator green.
    /// </summary>
    private static readonly Arbitrary<short> Quantities = Arb.From(Gen.Frequency(
    [
        (3, Gen.Elements(QuantityEdges())),
        (3, Sampled<Quantity, short>().Select(quantity => quantity.Value)),
        (2, ArbMap.Default.ArbFor<short>().Generator),
    ]));

    /// <summary>
    /// Unrounded amounts over a range a monetary value plausibly covers, weighted towards the exact
    /// half-cents where the rounding mode is the only thing that decides the answer. Deliberately not the
    /// whole of <see cref="decimal"/>: <c>Amount</c> pins the scale by adding <c>0.00m</c>, and near
    /// <see cref="decimal.MaxValue"/> a sum of two decimals cannot keep that scale within the 29 digits a
    /// decimal holds -- a property of this sample type's rounding rule, not of the framework, and not what
    /// these laws are about.
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

    /// <summary>Accepted amounts, drawn by the sampler across the whole range above the declared minimum, for the laws about how two of them relate.</summary>
    private static readonly Arbitrary<Amount> Amounts = Arb.From(Sampled<Amount, decimal>());

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

    /// <summary>
    /// Draws a value the type accepts through the FsCheck package's arbitrary, from a seed FsCheck draws, so that a failing
    /// run replays, one value in four at the edges the type declares.
    /// </summary>
    private static Gen<TSelf> Sampled<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => ValueObjectArbitrary.For<TSelf, TValue>(Sampling).Generator;

    /// <summary>
    /// The quantities at the edges of the declared range and one step past them, as the sampler derives them.
    /// </summary>
    private static short[] QuantityEdges()
    {
        var sampler = new ValueObjectSampler(new Random(0));

        return [.. sampler.Boundaries<Quantity, short>(), .. sampler.RejectedValues<Quantity, short>().Select(violation => violation.Value)];
    }

    /// <summary>
    /// A structurally valid IBAN: a country code, ISO 7064 MOD-97-10 check digits computed here rather
    /// than taken from the type under test, and a BBAN of a plausible length.
    /// </summary>
    private static string ElectronicIban(Random random)
    {
        string[] countries = ["FR", "DE", "BE", "NL", "ES", "IT", "LU", "PT", "GB"];
        var country = countries[random.Next(countries.Length)];
        var bban = string.Create(random.Next(11, 31), random, static (span, random) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Base36[random.Next(Base36.Length)];
            }
        });

        return country + CheckDigitsFor(country, bban) + bban;
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
