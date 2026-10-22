using AdCodicem.ValueObjects.Bogus;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Bogus;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>
/// The Bogus extensions: a rule for every value-object member of a faker, which StrictMode accepts, and a value object drawn
/// from a faker, each from the rules its type declares and replayed by UseSeed.
/// </summary>
public class BogusTests
{
    [Fact]
    public void Every_value_object_member_gets_a_rule_its_rules_accept_and_strict_mode_is_met()
    {
        var faker = new Faker<TestDataOrder>()
            .UseSeed(17)
            .StrictMode(true)
            .RuleForValueObjects()
            .RuleFor(order => order.Note, f => f.Lorem.Sentence());

        var orders = faker.Generate(20);

        foreach (var order in orders)
        {
            order.Backup.Should().NotBeNull("a nullable member gets a value");
            order.Contacts.Should().HaveCount(c => c >= 1 && c <= 3);
            order.Grades.Should().HaveCount(c => c >= 1 && c <= 3);
            order.Note.Should().NotBeNullOrEmpty();
            order.ValueObjects().ShouldAllBeAccepted();
            Quantity.TryCreate(order.Reserved.Value, out _).Should().BeTrue("a member behind a private setter gets a rule too");
        }

        orders.Select(order => order.Contacts.Count).Distinct().Should().HaveCountGreaterThan(1, "a list holds one to three values");
        orders.Select(order => order.Grades.Length).Distinct().Should().HaveCountGreaterThan(1, "an array holds one to three values");
    }

    [Fact]
    public void The_same_seed_gives_the_same_objects()
    {
        static List<string> Generate(int seed) => [.. new Faker<TestDataOrder>()
            .UseSeed(seed)
            .RuleForValueObjects()
            .RuleFor(order => order.Note, f => f.Lorem.Word())
            .Generate(5)
            .Select(order => string.Join('|', order.ValueObjects().Select(member => member.Value)) + "|" + order.Note)];

        var first = Generate(3);

        first.Should().Equal(Generate(3));
        first.Should().NotEqual(Generate(4));
    }

    [Fact]
    public void A_rule_written_after_wins_and_one_written_before_is_replaced()
    {
        var after = new Faker<TestDataOrder>().RuleForValueObjects().RuleFor(order => order.Quantity, _ => Quantity.Create(7));
        var before = new Faker<TestDataOrder>().RuleFor(order => order.Quantity, _ => Quantity.Create(7)).RuleForValueObjects();

        after.Generate(10).Should().AllSatisfy(order => order.Quantity.Value.Should().Be(7));
        before.Generate(20).Select(order => order.Quantity.Value).Should().Contain(quantity => quantity != 7);
    }

    [Fact]
    public void A_list_or_an_array_of_a_nullable_value_object_is_left_to_the_other_rules()
    {
        var ledger = new Faker<TestDataLedger>().RuleForValueObjects().Generate();

        ledger.Accounts.Should().BeEmpty();
        ledger.Quantities.Should().BeEmpty();
        ledger.Tally.Value.Should().BeInRange(1, 9, "a value object written by hand is resolved and drawn from its schema");
        Grade.TryCreate(ledger.Rank.Value, out _).Should().BeTrue("an internal field the binder binds gets a rule too");
        ValueObjectRegistry.TryGet(typeof(Declared<Tally, int>), out _).Should().BeTrue();
    }

    [Fact]
    public void The_options_reach_every_rule()
    {
        var vouchers = new Faker<TestDataVoucher>()
            .RuleForValueObjects(new ValueObjectSamplerOptions().Use<EvenCode, string>(EvenCodes.Draw))
            .Generate(20);

        vouchers.Should().AllSatisfy(voucher => EvenCodes.Validate(voucher.Code.Value).IsValid.Should().BeTrue());
    }

    [Fact]
    public void A_member_no_candidate_satisfies_fails_the_generation_naming_the_rule_to_write()
    {
        var generation = () => new Faker<TestDataVoucher>().RuleForValueObjects().Generate();

        var failure = generation.Should().Throw<ValueObjectSamplingException>().Which;
        failure.ErrorCode.Should().Be("checksum");
        failure.FromGenerator.Should().BeFalse();
        failure.Message.Should().EndWith("Register a generator of its underlying value: faker.ValueObject<EvenCode, string>(f => ...).");
    }

    [Fact]
    public void A_value_object_is_drawn_from_a_faker_without_naming_its_underlying_type()
    {
        var faker = new Faker { Random = new Randomizer(42) };

        var quantities = Enumerable.Range(0, 100).Select(_ => faker.ValueObject<Quantity>().Value).ToList();

        quantities.Should().AllSatisfy(quantity => quantity.Should().BeInRange((short)0, (short)1000));
        quantities.Distinct().Should().HaveCountGreaterThan(50);
        var seeded = new Faker { Random = new Randomizer(42) };
        Enumerable.Range(0, 100).Select(_ => seeded.ValueObject<Quantity>().Value).Should().Equal(quantities);
        new Faker().ValueObject<EvenCode>(new ValueObjectSamplerOptions().Use<EvenCode, string>(EvenCodes.Draw)).Value.Should().StartWith("EVEN-");
    }

    [Fact]
    public void A_type_that_is_no_value_object_is_refused_and_registers_nothing()
    {
        var draw = () => new Faker().ValueObject<MarkerOnlyValue>();

        draw.Should().Throw<ArgumentException>().WithMessage("'MarkerOnlyValue' is not a value object*");
        ValueObjectRegistry.TryGet(typeof(MarkerOnlyValue), out _).Should().BeFalse();
    }

    [Fact]
    public void A_value_object_no_candidate_satisfies_fails_naming_the_generator_to_give()
    {
        var withoutGenerator = () => new Faker().ValueObject<EvenCode>();
        var refusingGenerator = () => new Faker().ValueObject<EvenCode>(new ValueObjectSamplerOptions().Use<EvenCode, string>(_ => "EVEN-1"));

        withoutGenerator.Should().Throw<ValueObjectSamplingException>().Which.Message.Should()
            .EndWith("Register a generator of its underlying value: faker.ValueObject<EvenCode, string>(f => ...).");
        refusingGenerator.Should().Throw<ValueObjectSamplingException>().Which.Message.Should()
            .EndWith("Make the generator registered with options.Use<EvenCode, string>(random => ...) give values its rules accept.");
    }

    [Fact]
    public void A_generator_of_the_underlying_value_is_kept_only_once_the_type_accepts_its_values()
    {
        var faker = new Faker { Random = new Randomizer(8) };

        var ibans = Enumerable.Range(0, 20).Select(_ => faker.ValueObject<Iban, string>(f => f.Finance.Iban())).ToList();

        ibans.Should().AllSatisfy(iban => Iban.TryCreate(iban.Value, out _).Should().BeTrue());
        ibans.Distinct().Should().HaveCountGreaterThan(1, "Bogus's IBANs carry valid check digits, so the example is not what comes out");
        faker.ValueObject<Quantity, short>(_ => 7).Value.Should().Be(7, "the generator gives the candidates");
        faker.ValueObject<Iban, string>(_ => "not an IBAN").Should().Be(Iban.Example, "a type that declares an example falls back on it");
    }

    [Fact]
    public void A_generator_whose_values_are_always_refused_fails_naming_itself()
    {
        var draw = () => new Faker().ValueObject<EvenCode, string>(_ => "EVEN-1");

        var failure = draw.Should().Throw<ValueObjectSamplingException>().Which;
        failure.FromGenerator.Should().BeTrue();
        failure.ErrorCode.Should().Be("checksum");
        failure.Message.Should().EndWith("Make the generator registered with faker.ValueObject<EvenCode, string>(f => ...) give values its rules accept.");
    }

    [Fact]
    public void The_extensions_refuse_missing_arguments()
    {
        var withoutFaker = () => ((Faker)null!).ValueObject<Quantity>();
        var withoutFakerForGenerator = () => ((Faker)null!).ValueObject<Quantity, short>(_ => 1);
        var withoutGenerator = () => new Faker().ValueObject<Quantity, short>(null!);
        var withoutFakerOfT = () => ((Faker<TestDataOrder>)null!).RuleForValueObjects();

        withoutFaker.Should().Throw<ArgumentNullException>().WithParameterName("faker");
        withoutFakerForGenerator.Should().Throw<ArgumentNullException>().WithParameterName("faker");
        withoutGenerator.Should().Throw<ArgumentNullException>().WithParameterName("generator");
        withoutFakerOfT.Should().Throw<ArgumentNullException>().WithParameterName("faker");
    }
}

/// <summary>A voucher whose code carries a rule no schema carries.</summary>
public sealed class TestDataVoucher
{
    public EvenCode Code { get; set; }
}
