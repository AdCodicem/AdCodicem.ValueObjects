// The AutoFixture, Bogus and FsCheck packages compile their own copy of TypeNames, which the alias keeps apart.
extern alias testingdata;

using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;
using TypeNames = testingdata::AdCodicem.ValueObjects.Shared.TypeNames;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>
/// The test-data sampler on each rule of a schema it draws from: known values, lengths, patterns, bounds, the generators
/// registered for a rule no schema carries, and what it does once no candidate passes. Every draw here uses a seed, so that
/// a share counted over many draws is the same on every run.
/// </summary>
public class SamplerTests
{
    private const int Draws = 500;

    [Fact]
    public void A_closed_value_set_is_drawn_from_its_known_values_alone()
    {
        var drawn = Draw<CountryCode, string>(new ValueObjectSampler(new Random(3)));

        drawn.Select(country => country.Value).Distinct().Should().BeEquivalentTo(["FR", "BE", "LU"]);

        // Every candidate is a known value: one attempt is enough, and nothing falls back on an example it does not have.
        var once = new ValueObjectSampler(new Random(3), new ValueObjectSamplerOptions { MaxAttempts = 1 });
        once.Invoking(sampler => Draw<CountryCode, string>(sampler)).Should().NotThrow();
    }

    [Theory]
    [InlineData(0d, 0, 0)]
    [InlineData(1d, Draws, Draws)]
    [InlineData(0.5, 200, 300)]
    public void An_open_value_set_draws_its_known_values_the_share_the_options_ask(double share, int fewest, int most)
    {
        var sampler = new ValueObjectSampler(new Random(4), new ValueObjectSamplerOptions { KnownValueShare = share });

        var known = Draw<PageNumber, int>(sampler).Count(page => page == PageNumber.First);

        known.Should().BeInRange(fewest, most);
    }

    [Fact]
    public void A_string_keeps_to_its_declared_lengths_and_draws_ASCII_letters_and_digits_without_a_pattern()
    {
        var references = Draw<Ordering.OrderReference, string>(new ValueObjectSampler(new Random(5))).Select(reference => reference.Value).ToList();

        references.Should().OnlyContain(reference => reference.Length >= 3 && reference.Length <= 20);
        references.Select(reference => reference.Length).Should().Contain([3, 20], "every length is drawn, the edges included");
        references.Should().OnlyContain(reference => reference.All(char.IsAsciiLetterOrDigit));
    }

    [Fact]
    public void A_string_without_a_maximum_length_gets_at_most_the_extra_length_the_options_allow()
    {
        Draw<FreeText, string>(new ValueObjectSampler(new Random(6))).Should()
            .OnlyContain(text => text.Value.Length >= 1 && text.Value.Length <= 33, "never empty, at most 32 beyond one character");
        Draw<Remark, string>(new ValueObjectSampler(new Random(6))).Should()
            .OnlyContain(remark => remark.Value.Length >= 4 && remark.Value.Length <= 36);
        Draw<Remark, string>(new ValueObjectSampler(new Random(6), new ValueObjectSamplerOptions { MaxExtraLength = 0 })).Should()
            .OnlyContain(remark => remark.Value.Length == 4);
        Draw<NamedGroup, string>(new ValueObjectSampler(new Random(6))).Should()
            .OnlyContain(code => code.Value.Length >= 3 && code.Value.Length <= 35, "at most 32 beyond the shortest match of its pattern")
            .And.Contain(code => code.Value.Length > 33);
    }

    /// <summary>
    /// Without a maximum length, the extra length is counted from the shortest match of the pattern: a pattern longer than
    /// the declared minimum and the extra length together is still drawn whole, rather than falling back on nothing.
    /// </summary>
    [Theory]
    [InlineData(typeof(Digest), 64)]
    [InlineData(typeof(UuidText), 36)]
    [InlineData(typeof(LongToken), 40)]
    public void A_pattern_longer_than_the_extra_length_is_drawn_whole_without_a_maximum_length(Type type, int length)
    {
        var descriptor = ValueObjectRegistry.TryGet(type, out var registered) ? registered : throw new InvalidOperationException();
        var sampler = new ValueObjectSampler(new Random(39));
        var pattern = new Regex(descriptor.Schema.Pattern!, RegexOptions.CultureInvariant);

        var drawn = Enumerable.Range(0, 200).Select(_ => (string)descriptor.GetValue(sampler.Next(descriptor))!).ToList();

        drawn.Should().OnlyContain(text => text.Length == length && pattern.IsMatch(text));
        drawn.Distinct().Should().HaveCount(200);
    }

    [Fact]
    public void A_type_that_accepts_the_empty_string_never_draws_it_but_has_it_among_its_boundaries()
    {
        var sampler = new ValueObjectSampler(new Random(7));

        Draw<Label, string>(sampler).Should().OnlyContain(label => label.Value.Length >= 1 && label.Value.Length <= 200);
        sampler.Boundaries<Label, string>().Select(label => label.Length).Should().Equal(0, 200);
    }

    /// <summary>
    /// Candidates come from the schema rather than from the type's rules filtering arbitrary values: every draw passes at
    /// its first candidate, which the normalizer, run once on each candidate, counts.
    /// </summary>
    [Fact]
    public void Every_candidate_drawn_from_lengths_a_pattern_or_bounds_is_accepted_at_once()
    {
        var sampler = new ValueObjectSampler(new Random(8));

        CountedCode.ResetCalls();
        var codes = Draw<CountedCode, string>(sampler);
        CountedCode.Calls.Should().Be(Draws);
        codes.Should().OnlyContain(code => code.Value.Length >= 3 && code.Value.Length <= 8);

        CountedNumber.ResetCalls();
        var numbers = Draw<CountedNumber, int>(sampler);
        CountedNumber.Calls.Should().Be(Draws);
        numbers.Select(number => number.Value).Distinct().Should().BeEquivalentTo(Enumerable.Range(-5, 11), "every value of the range is drawn");
    }

    [Theory]
    [InlineData(typeof(UpperThree))]
    [InlineData(typeof(PasswordLike))]
    [InlineData(typeof(PasswordLikeWithoutLengths))]
    [InlineData(typeof(NamedGroup))]
    [InlineData(typeof(Alternated))]
    [InlineData(typeof(Dashed))]
    [InlineData(typeof(Subtracted))]
    [InlineData(typeof(WordBounded))]
    [InlineData(typeof(HoldsADigit))]
    [InlineData(typeof(Iban))]
    [InlineData(typeof(EmailAddress))]
    [InlineData(typeof(PhoneNumber))]
    public void Every_string_drawn_matches_the_pattern_of_its_type(Type type)
    {
        var descriptor = ValueObjectRegistry.TryGet(type, out var registered) ? registered : throw new InvalidOperationException();
        var sampler = new ValueObjectSampler(new Random(9));
        var pattern = new Regex(descriptor.Schema.Pattern!, RegexOptions.CultureInvariant);

        var drawn = Enumerable.Range(0, 200).Select(_ => (string)descriptor.GetValue(sampler.Next(descriptor))!).ToList();

        drawn.Should().OnlyContain(text => pattern.IsMatch(text));
        drawn.Distinct().Should().HaveCountGreaterThan(20, "{0} is drawn, not a constant", type.Name);
    }

    [Fact]
    public void A_pattern_whose_matches_are_shorter_than_the_declared_lengths_is_padded_at_an_end_no_anchor_holds()
    {
        var sampler = new ValueObjectSampler(new Random(10));

        Draw<HoldsADigit, string>(sampler).Should().OnlyContain(text => text.Value.Length >= 5 && text.Value.Any(char.IsAsciiDigit));

        var addresses = Draw<CorporateEmail, string>(sampler);
        addresses.Should().OnlyContain(address => address.Value.Length >= 12 && address.Value.EndsWith("@acme.com", StringComparison.Ordinal));
        addresses.Count(address => address == CorporateEmail.Example).Should().BeLessThan(Draws / 20, "the example is no fallback here");

        var numbers = Draw<DialledNumber, string>(sampler);
        numbers.Should().OnlyContain(number => number.Value.Length >= 10 && Regex.IsMatch(number.Value, @"^[0-9]{3}-[0-9]{4}"));
        numbers.Count(number => number == DialledNumber.Example).Should().BeLessThan(Draws / 20);

        var signs = Draw<HoldsAnAtSign, string>(sampler).Select(text => text.Value).ToList();
        signs.Should().OnlyContain(text => text.Length >= 3 && text.Length <= 35 && text.Contains('@', StringComparison.Ordinal));
        signs.Select(text => text.Length).Distinct().Should().HaveCountGreaterThan(20, "a match is padded to any length up to 32 beyond the minimum");
        signs.Should().Contain(text => text.StartsWith('@')).And.Contain(text => text.EndsWith('@')).And.Contain(text => text[1] == '@');

        sampler.Boundaries<CorporateEmail, string>().Should().ContainSingle().Which.Should().HaveLength(12).And.EndWith("@acme.com");
    }

    /// <summary>
    /// Rejection sampling draws from the pattern read without its lookarounds, and with its categories reduced to
    /// printable ASCII, rather than from letters and digits a pattern needing an at sign or a dash never matches.
    /// </summary>
    [Fact]
    public void A_pattern_outside_the_subset_is_drawn_from_its_loose_reading_and_kept_once_it_matches()
    {
        var sampler = new ValueObjectSampler(new Random(40));

        var addresses = Draw<GuardedEmail, string>(sampler);
        addresses.Should().OnlyContain(address => GuardedEmail.Pattern.IsMatch(address.Value));
        addresses.Count(address => address == GuardedEmail.Example).Should().BeLessThan(Draws / 2);
        addresses.Distinct().Should().HaveCountGreaterThan(Draws / 2);

        var plates = Draw<Plate, string>(sampler);
        plates.Should().OnlyContain(plate => Regex.IsMatch(plate.Value, "^[A-Z]{2}-[0-9]{3}-[A-Z]{2}$"));
        plates.Count(plate => plate == Plate.Example).Should().BeLessThan(Draws / 20);
        sampler.Boundaries<Plate, string>().Should().NotBeEmpty().And.OnlyContain(plate => Regex.IsMatch(plate, "^[A-Z]{2}-[0-9]{3}-[A-Z]{2}$"));
    }

    [Fact]
    public void A_pattern_outside_the_subset_is_met_at_an_edge_length_by_rejection_sampling()
    {
        var sampler = new ValueObjectSampler(new Random(37));

        sampler.Boundaries<UpperThree, string>().Should().NotBeEmpty().And.OnlyContain(text => Regex.IsMatch(text, "^[A-Z]{3}$"));
        sampler.RejectedValues<PasswordLike, string>().Select(violation => violation.Value.Length).Should().Equal(7, 0, 9);
    }

    [Fact]
    public void A_generator_registered_for_a_type_replaces_its_schema_and_is_retried_until_the_type_accepts_a_value()
    {
        var calls = 0;
        var options = new ValueObjectSamplerOptions().Use<EvenCode, string>(random => ++calls % 2 == 0 ? EvenCodes.Draw(random) : "EVEN-1");

        var drawn = Draw<EvenCode, string>(new ValueObjectSampler(new Random(11), options));

        drawn.Should().OnlyContain(code => code.Value.StartsWith("EVEN-", StringComparison.Ordinal));
        calls.Should().Be(2 * Draws, "every other value the generator gives is refused, and drawn again");
    }

    [Fact]
    public void A_later_registration_for_a_type_replaces_the_earlier_one()
    {
        var options = new ValueObjectSamplerOptions()
            .Use<EvenCode, string>(_ => "EVEN-11")
            .Use<EvenCode, string>(_ => "EVEN-22");

        new ValueObjectSampler(new Random(12), options).Next<EvenCode, string>().Value.Should().Be("EVEN-22");
    }

    [Fact]
    public void A_type_whose_rules_refuse_every_candidate_falls_back_on_its_example()
    {
        Draw<ExampledEvenCode, string>(new ValueObjectSampler(new Random(13))).Should().OnlyContain(code => code == ExampledEvenCode.Example);
    }

    [Fact]
    public void A_type_whose_rules_refuse_every_candidate_and_declare_no_example_fails_with_the_registration_to_add()
    {
        var sampler = new ValueObjectSampler(new Random(14), new ValueObjectSamplerOptions { MaxAttempts = 25 });

        var failure = sampler.Invoking(sampler => sampler.Next<EvenCode, string>()).Should().Throw<ValueObjectSamplingException>().Which;

        failure.Message.Should().Be(
            "Could not draw a value of 'EvenCode' its rules accept: the last of 25 candidates drawn from its schema was refused "
            + "(checksum), and it declares no example its rules accept. Register a generator of its underlying value: "
            + "options.Use<EvenCode, string>(random => ...).");
        failure.Should().BeAssignableTo<InvalidOperationException>();
        failure.ValueObjectType.Should().Be<EvenCode>();
        failure.Attempts.Should().Be(25);
        failure.ErrorCode.Should().Be("checksum");
        failure.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse("a failure of test set-up is no refusal at a boundary");
        ValueObjectErrors.TryGetCode(failure, out _).Should().BeFalse();
    }

    [Fact]
    public void A_registered_generator_whose_values_its_type_refuses_fails_naming_that_generator()
    {
        var options = new ValueObjectSamplerOptions { MaxAttempts = 3 }.Use<EvenCode, string>(_ => "EVEN-1");

        var failure = new ValueObjectSampler(new Random(41), options).Invoking(sampler => sampler.Next<EvenCode, string>())
            .Should().Throw<ValueObjectSamplingException>().Which;

        failure.Message.Should().Be(
            "Could not draw a value of 'EvenCode' its rules accept: the last of 3 candidates its registered generator gave was "
            + "refused (checksum), and it declares no example its rules accept. Make the generator registered with "
            + "options.Use<EvenCode, string>(random => ...) give values its rules accept.");
        failure.FromGenerator.Should().BeTrue();
        failure.ErrorCode.Should().Be("checksum");

        var uncoded = new ValueObjectSamplingException(typeof(EvenCode), 3, null, "registration", fromGenerator: true);
        uncoded.Message.Should().Contain("its registered generator gave was refused, and");
        new ValueObjectSamplingException(typeof(EvenCode), 3, null, "registration").FromGenerator.Should().BeFalse();
    }

    [Fact]
    public void A_type_the_sampler_draws_no_value_of_fails_without_a_code_until_a_generator_is_registered()
    {
        var failure = new ValueObjectSampler(new Random(15)).Invoking(sampler => sampler.Next<Declared<Link, Uri>, Uri>())
            .Should().Throw<ValueObjectSamplingException>().Which;

        failure.Message.Should().Be(
            "Could not draw a value of 'Declared<Link, Uri>' its rules accept: its schema gave no candidate in 100 attempts, and "
            + "it declares no example its rules accept. Register a generator of its underlying value: "
            + "options.Use<Declared<Link, Uri>, Uri>(random => ...).");
        failure.ErrorCode.Should().BeNull();

        var options = new ValueObjectSamplerOptions().Use<Declared<Link, Uri>, Uri>(random => new Uri($"https://example.com/{random.Next(100)}"));
        new ValueObjectSampler(new Random(15), options).Next<Declared<Link, Uri>, Uri>().Value.Host.Should().Be("example.com");
    }

    [Fact]
    public void TryNext_reports_the_refusal_of_the_last_candidate_or_none_when_the_schema_gave_no_candidate()
    {
        var sampler = new ValueObjectSampler(new Random(16));

        sampler.TryNext<EvenCode, string>(out var code, out var refusal).Should().BeFalse();
        code.Value.Should().BeEmpty("no value was drawn");
        refusal.ErrorCode.Should().Be("checksum");

        sampler.TryNext<Declared<Link, Uri>, Uri>(out _, out refusal).Should().BeFalse();
        refusal.IsValid.Should().BeTrue("no candidate was refused");

        sampler.TryNext<Quantity, short>(out var quantity, out refusal).Should().BeTrue();
        Quantity.TryCreate(quantity.Value, out _).Should().BeTrue();
        refusal.IsValid.Should().BeTrue();

        sampler.TryNext<ExampledEvenCode, string>(out var example, out refusal).Should().BeTrue();
        example.Should().Be(ExampledEvenCode.Example);
        refusal.IsValid.Should().BeTrue("the example was accepted");
    }

    [Fact]
    public void A_seed_replays_its_draws()
    {
        Draw<Iban, string>(new ValueObjectSampler(new Random(17))).Should().Equal(Draw<Iban, string>(new ValueObjectSampler(new Random(17))));
    }

    [Fact]
    public void Without_a_generator_an_IBAN_falls_back_on_its_example_for_a_third_of_its_draws()
    {
        // Why PropertyTests registers its generator: the MOD-97 check digits are a rule no schema carries.
        var examples = Draw<Iban, string>(new ValueObjectSampler(new Random(18))).Count(iban => iban == Iban.Example);

        examples.Should().BeGreaterThan(Draws / 5);
    }

    [Fact]
    public void A_descriptor_draws_a_construction_of_a_generic_value_object()
    {
        ValueObjectRegistry.TryResolve(typeof(Reference<PurchaseOrder>), out var descriptor).Should().BeTrue();

        var drawn = new ValueObjectSampler(new Random(19)).Next(descriptor!);

        drawn.Should().BeOfType<Reference<PurchaseOrder>>().Which.Value.Length.Should().BeInRange(1, 12);
    }

    [Fact]
    public void A_schema_written_by_hand_is_drawn_from_as_far_as_it_can_be_read()
    {
        var sampler = new ValueObjectSampler(new Random(20));

        // An example written as text, parsed as the type parses it.
        Draw<Declared<TextExample, int>, int>(sampler).Should().OnlyContain(value => value.Value == 42);

        // A pattern .NET does not compile checks nothing: the candidates keep to the declared lengths.
        Draw<Declared<InvalidPattern, string>, string>(sampler).Should().OnlyContain(value => value.Value.Length >= 2 && value.Value.Length <= 6);

        // A bound read as an infinity, or not read at all, bounds nothing.
        Draw<Declared<UnreadableBounds, double>, double>(sampler).Select(value => value.Value).Should()
            .Contain(value => value < 0).And.Contain(value => value > 1);
        sampler.Boundaries<Declared<UnreadableBounds, double>, double>().Should().BeEmpty();
        sampler.RejectedValues<Declared<UnreadableBounds, double>, double>().Should().BeEmpty();

        // Known values of another type than the underlying one are left out.
        Draw<Declared<MistypedKnownValues, int>, int>(sampler).Should().OnlyContain(value => value.Value == 3);

        // Known values left at the default of their array are none.
        Draw<Declared<DefaultKnownValues, int>, int>(sampler).Select(value => value.Value).Distinct().Should().HaveCount(10);
    }

    /// <summary>
    /// Bounds the wrong way round hold no value: a real or a decimal is drawn as the lower bound, which the rules refuse,
    /// rather than an exception thrown from the draw, as for an integer.
    /// </summary>
    [Fact]
    public void Bounds_the_wrong_way_round_draw_a_candidate_the_rules_refuse()
    {
        var sampler = new ValueObjectSampler(new Random(42), new ValueObjectSamplerOptions { MaxAttempts = 5 });

        sampler.TryNext<Declared<InvertedReal, double>, double>(out _, out var refusal).Should().BeFalse();
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        sampler.TryNext<Declared<InvertedDecimal, decimal>, decimal>(out _, out refusal).Should().BeFalse();
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        sampler.Invoking(sampler => sampler.Next<Declared<InvertedReal, double>, double>())
            .Should().Throw<ValueObjectSamplingException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_string_no_longer_than_nothing_is_the_empty_string_and_one_character_breaks_it()
    {
        var sampler = new ValueObjectSampler(new Random(21));

        sampler.Next<Declared<ZeroLength, string>, string>().Value.Should().BeEmpty();
        sampler.RejectedValues<Declared<ZeroLength, string>, string>().Should().Equal(new SchemaViolation<string>("A", "MaxLength (0)"));
    }

    [Fact]
    public void Lengths_below_zero_derive_nothing_and_draw_the_empty_string()
    {
        var sampler = new ValueObjectSampler(new Random(22));

        sampler.Next<Declared<NegativeLength, string>, string>().Value.Should().BeEmpty();
        sampler.Boundaries<Declared<NegativeLength, string>, string>().Should().Equal(string.Empty);
        sampler.RejectedValues<Declared<NegativeLength, string>, string>().Should().BeEmpty();
    }

    [Fact]
    public void A_character_keeps_to_printable_ASCII_on_a_side_left_open_unless_the_other_bound_lies_beyond_it()
    {
        var sampler = new ValueObjectSampler(new Random(23));

        Draw<UnboundedChar, char>(sampler).Should().OnlyContain(character => character.Value >= ' ' && character.Value <= '~');
        Draw<LowerChar, char>(sampler).Should().OnlyContain(character => character.Value >= ' ' && character.Value <= 'z');
        Draw<ControlChar, char>(sampler).Should().OnlyContain(character => character.Value <= (char)0x1F)
            .And.Contain(character => character.Value < ' ');
        Draw<AccentedChar, char>(sampler).Should().OnlyContain(character => character.Value >= (char)0xE9)
            .And.Contain(character => character.Value > (char)0xFF, "the open side reaches the type's maximum");
    }

    [Fact]
    public void A_custom_pattern_sampler_is_tried_first_and_leaves_a_pattern_it_cannot_sample_to_the_built_in_one()
    {
        var asked = new List<string>();
        var options = new ValueObjectSamplerOptions
        {
            PatternSampler = (pattern, random) =>
            {
                asked.Add(pattern);
                return pattern.Contains('-', StringComparison.Ordinal) ? $"ABC-{random.Next(100, 1000)}" : null;
            },
        };
        var sampler = new ValueObjectSampler(new Random(24), options);

        sampler.Next<Dashed, string>().Value.Should().MatchRegex("^ABC-[0-9]{3}$");
        sampler.Next<Alternated, string>().Value.Should().MatchRegex("^(FR|BE)[0-9]{2}$");
        asked.Should().Equal(Dashed.Schema.Pattern, Alternated.Schema.Pattern);
    }

    [Fact]
    public void The_options_refuse_what_no_draw_can_take()
    {
        var options = new ValueObjectSamplerOptions { KnownValueShare = 0, MaxExtraLength = 0, MaxAttempts = 1 };
        options.KnownValueShare = 1;

        options.Invoking(options => options.MaxAttempts = 0).Should().Throw<ArgumentOutOfRangeException>();
        options.Invoking(options => options.KnownValueShare = -0.1).Should().Throw<ArgumentOutOfRangeException>();
        options.Invoking(options => options.KnownValueShare = 1.1).Should().Throw<ArgumentOutOfRangeException>();
        options.Invoking(options => options.KnownValueShare = double.NaN).Should().Throw<ArgumentOutOfRangeException>();
        options.Invoking(options => options.MaxExtraLength = -1).Should().Throw<ArgumentOutOfRangeException>();
        options.Invoking(options => options.Use<EvenCode, string>(null!)).Should().Throw<ArgumentNullException>();
        options.Should().BeEquivalentTo(new { KnownValueShare = 1d, MaxExtraLength = 0, MaxAttempts = 1 });

        FluentActions.Invoking(() => new ValueObjectSampler(null!)).Should().Throw<ArgumentNullException>();
        new ValueObjectSampler(Random.Shared).Invoking(sampler => sampler.Next(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ValueObjectSamplingException(null!, 1, null, "registration")).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ValueObjectSamplingException(typeof(EvenCode), 1, null, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void The_boundaries_are_the_accepted_values_at_the_edges_the_schema_declares()
    {
        var sampler = new ValueObjectSampler(new Random(25));

        sampler.Boundaries<Quantity, short>().Should().Equal(0, 1000);
        sampler.Boundaries<Price, decimal>().Should().Equal(0.01m, 999.99m);
        sampler.Boundaries<PageNumber, int>().Should().Equal(1);
        sampler.Boundaries<CountryCode, string>().Should().Equal("FR", "BE", "LU");
        sampler.Boundaries<DocumentStatus, string>().Should().Equal("draft", "final");
        sampler.Boundaries<TermsAccepted, bool>().Should().Equal(true);
        sampler.Boundaries<Consent, bool>().Should().BeEmpty();
        sampler.Boundaries<Iban, string>().Should().BeEmpty("no string of its edge lengths drawn from its pattern has its check digits");
        sampler.Boundaries<OpeningTime, TimeOnly>().Should().Equal(new TimeOnly(6, 0), new TimeOnly(12, 0));
        sampler.Boundaries<Ordering.OrderReference, string>().Select(reference => reference.Length).Should().Equal(3, 20);
    }

    [Fact]
    public void The_rejected_values_are_one_step_past_each_rule_the_schema_declares()
    {
        var sampler = new ValueObjectSampler(new Random(26));

        sampler.RejectedValues<Quantity, short>().Should().Equal(new SchemaViolation<short>(-1, "Minimum (0)"), new(1001, "Maximum (1000)"));
        sampler.RejectedValues<Price, decimal>().Should().Equal(new SchemaViolation<decimal>(0.00m, "Minimum (0.01)"), new(1000.00m, "Maximum (999.99)"));
        sampler.RejectedValues<Grade, char>().Should().Equal(new SchemaViolation<char>('@', "Minimum (A)"), new('G', "Maximum (F)"));
        sampler.RejectedValues<Latitude, double>().Should().Equal(
            new SchemaViolation<double>(Math.BitDecrement(-90d), "Minimum (-90)"),
            new(Math.BitIncrement(90d), "Maximum (90)"));
        sampler.RejectedValues<BirthDate, DateOnly>().Should().Equal(
            new SchemaViolation<DateOnly>(new DateOnly(1899, 12, 31), "Minimum (1900-01-01)"),
            new(new DateOnly(2101, 1, 1), "Maximum (2100-12-31)"));
        sampler.RejectedValues<Duration, TimeSpan>().Select(violation => violation.Value).Should().Equal(
            TimeSpan.FromTicks(-1), TimeSpan.FromDays(1) + TimeSpan.FromTicks(1));
        sampler.RejectedValues<TermsAccepted, bool>().Should().Equal(new SchemaViolation<bool>(false, "a closed value set"));
    }

    [Fact]
    public void A_string_of_a_wrong_length_is_otherwise_drawn_as_its_pattern_asks()
    {
        var violations = new ValueObjectSampler(new Random(27)).RejectedValues<Iban, string>().ToList();

        violations.Select(violation => violation.Rule).Should().Equal("MinLength (15)", "MinLength (15)", "MaxLength (34)");
        violations.Select(violation => violation.Value.Length).Should().Equal(14, 0, 35);
        violations[0].Value.Should().MatchRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{10}$");
        violations[2].Value[..34].Should().MatchRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{30}$");
    }

    /// <summary>
    /// A string of a wrong length is drawn again while the type's normalizer brings it back to a right one, as trimming
    /// does a space the pattern let the draw put at an end: every such value is refused, whatever the seed.
    /// </summary>
    [Fact]
    public void A_string_of_a_wrong_length_keeps_its_wrong_length_once_normalized()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var sampler = new ValueObjectSampler(new Random(seed));

            foreach (var (value, rule) in sampler.RejectedValues<Comment, string>())
            {
                Comment.TryCreate(value, out _).Should().BeFalse("'{0}' breaks {1} (seed {2})", value, rule, seed);
            }

            foreach (var (value, rule) in sampler.RejectedValues<PersonName, string>())
            {
                PersonName.TryCreate(value, out _).Should().BeFalse("'{0}' breaks {1} (seed {2})", value, rule, seed);
            }
        }
    }

    /// <summary>
    /// A normalizer that cuts every string to the length, rather than a validator refusing a longer one, keeps no draw to
    /// a wrong length: the last one is still derived, and the type accepts it, which the contract kit reports.
    /// </summary>
    [Fact]
    public void A_type_that_cuts_a_string_to_its_length_still_has_a_value_past_it_derived()
    {
        var violation = new ValueObjectSampler(new Random(43)).RejectedValues<CutCode, string>().Should().ContainSingle().Which;

        violation.Rule.Should().Be("MaxLength (5)");
        violation.Value.Should().HaveLength(6);
        CutCode.TryCreate(violation.Value, out _).Should().BeTrue();

        // A normalizer that gives nothing back keeps no draw either.
        new ValueObjectSampler(new Random(43)).RejectedValues<Declared<NullNormalized, string>, string>()
            .Should().ContainSingle().Which.Value.Should().HaveLength(4);
    }

    /// <summary>
    /// A bound written with an offset is an instant: the values drawn lie between the instants, and one tick before and
    /// after them is derived, whatever the offsets.
    /// </summary>
    [Fact]
    public void An_instant_bounded_with_offsets_is_drawn_and_stepped_as_an_instant()
    {
        var sampler = new ValueObjectSampler(new Random(44));

        Draw<OffsetBounded, DateTimeOffset>(sampler).Should()
            .OnlyContain(instant => instant.Value >= OffsetBounded.Minimum && instant.Value <= OffsetBounded.Maximum);
        sampler.Boundaries<OffsetBounded, DateTimeOffset>().Should().Equal(OffsetBounded.Minimum, OffsetBounded.Maximum);
        sampler.RejectedValues<OffsetBounded, DateTimeOffset>().Select(violation => violation.Value.UtcTicks).Should().Equal(
            OffsetBounded.Minimum.UtcTicks - 1,
            OffsetBounded.Maximum.UtcTicks + 1);
    }

    [Fact]
    public void A_value_outside_a_closed_set_is_compared_with_its_known_values_once_normalized_and_ignoring_case()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var outside = new ValueObjectSampler(new Random(seed)).RejectedValues<CountryCode, string>().Single(violation => violation.Rule == "a closed value set");

            outside.Value.Should().HaveLength(2);
            CountryCode.TryCreate(outside.Value, out _).Should().BeFalse();
        }
    }

    [Fact]
    public void A_value_outside_a_closed_set_compared_ignoring_case_is_none_of_its_spellings()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var outside = new ValueObjectSampler(new Random(seed)).RejectedValues<CaseFolded, string>().Single(violation => violation.Rule == "a closed value set");

            CaseFolded.TryCreate(outside.Value, out _).Should().BeFalse("'{0}' was derived outside the set", outside.Value);
        }
    }

    [Fact]
    public void A_closed_set_whose_normalizer_maps_every_value_onto_a_member_has_no_value_outside_it()
        => new ValueObjectSampler(new Random(38)).RejectedValues<Everything, string>().Should().BeEmpty();

    [Fact]
    public void Nothing_is_derived_past_the_extremes_of_the_type_or_outside_a_set_that_holds_every_value()
    {
        var sampler = new ValueObjectSampler(new Random(28));

        sampler.RejectedValues<ExtremeBounds, int>().Should().BeEmpty();
        sampler.RejectedValues<CappedDecimal, decimal>().Should().BeEmpty();
        sampler.RejectedValues<MidnightTime, TimeOnly>().Should().BeEmpty("no step wraps around to the evening before");
        sampler.RejectedValues<EveryAnswer, bool>().Should().BeEmpty();
        sampler.RejectedValues<Declared<ClosedLinks, Uri>, Uri>().Should().BeEmpty("no link is drawn outside the set");
        sampler.RejectedValues<AnyCharacter, char>().Should().BeEmpty("no step is taken past the first or the last character");
        sampler.RejectedValues<LatestTime, TimeOnly>().Should().BeEmpty("no step wraps around to midnight");
        sampler.RejectedValues<LatestDate, DateOnly>().Should().BeEmpty();
        sampler.RejectedValues<LatestInstant, DateTime>().Should().BeEmpty();
        sampler.RejectedValues<AnyDuration, TimeSpan>().Should().BeEmpty();
        Draw<CappedDecimal, decimal>(sampler).Select(value => value.Value).Should().Contain(value => value < 0).And.Contain(value => value > 0);
    }

    [Fact]
    public void A_decimal_left_unbounded_is_drawn_across_the_whole_type()
    {
        var drawn = Draw<UnboundedDecimal, decimal>(new ValueObjectSampler(new Random(29))).Select(value => value.Value).ToList();

        drawn.Should().Contain(value => value < -1e27m).And.Contain(value => value > 1e27m);
    }

    [Fact]
    public void A_string_its_pattern_cannot_draw_at_an_edge_length_is_broken_by_the_length_alone()
    {
        var sampler = new ValueObjectSampler(new Random(30));

        sampler.Boundaries<OddPairs, string>().Should().BeEmpty();
        sampler.RejectedValues<OddPairs, string>().Should().Equal(
            new SchemaViolation<string>("AA", "MinLength (3)"),
            new(string.Empty, "MinLength (3)"),
            new("AAAA", "MaxLength (3)"));
    }

    [Fact]
    public void The_names_in_a_message_are_written_as_CSharp_writes_them()
    {
        TypeNames.Of(typeof(string)).Should().Be("string");
        TypeNames.Of(typeof(Int128)).Should().Be("Int128");
        TypeNames.Of(typeof(Ordering.OrderReference)).Should().Be("Ordering.OrderReference");
        TypeNames.Of(typeof(Reference<PurchaseOrder>)).Should().Be("Reference<PurchaseOrder>");
        TypeNames.Of(typeof(Catalog<string>.Stock)).Should().Be("Catalog<string>.Stock");
        TypeNames.Of(typeof(Dictionary<int, string?[]>)).Should().Be("Dictionary<int, string[]>");
        TypeNames.Of(typeof(int?[,])).Should().Be("int?[,]");
        TypeNames.Of(typeof(Reference<>).GetGenericArguments()[0]).Should().Be("TOwner");
    }

    private static List<TSelf> Draw<TSelf, TValue>(ValueObjectSampler sampler)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => [.. Enumerable.Range(0, Draws).Select(_ => sampler.Next<TSelf, TValue>())];
}
