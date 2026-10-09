using System.Globalization;
using AdCodicem.ValueObjects.AutoFixture;
using AdCodicem.ValueObjects.Bogus;
using AdCodicem.ValueObjects.FsCheck;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;
using AutoFixture;
using Bogus;
using FsCheck;
using FsCheck.Fluent;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The test-data sampler of the package as packed, drawing the value objects the generator of the next SDK wrote, from the
/// schemas it wrote for them, and the AutoFixture, Bogus and FsCheck packages over it.
/// </summary>
public sealed class TestDataTests
{
    /// <summary>The check digits of an IBAN, a rule no schema carries, drawn by a generator registered with the sampler.</summary>
    private static readonly ValueObjectSamplerOptions Options = new ValueObjectSamplerOptions().Use<Iban, string>(DrawIban);

    [Fact]
    public void Every_value_object_of_the_domain_is_drawn_as_values_its_rules_accept()
    {
        var sampler = new ValueObjectSampler(new Random(1), Options);

        Draws<CustomerId, Guid>(sampler);
        Draws<EmailAddress, string>(sampler);
        Draws<Iban, string>(sampler);
        Draws<Amount, decimal>(sampler);
        Draws<Quantity, int>(sampler);
        Draws<BirthDate, DateOnly>(sampler);
        Draws<Ratio, double>(sampler);
        Draws<CountryCode, string>(sampler);
        Draws<PaymentId, string>(sampler);
        Draws<Reference<PurchaseOrder>, string>(sampler);
    }

    [Fact]
    public void A_descriptor_draws_a_construction_of_a_generic_value_object()
    {
        ValueObjectRegistry.TryResolve(typeof(Reference<SalesInvoice>), out var descriptor).Should().BeTrue();

        new ValueObjectSampler(new Random(2)).Next(descriptor!).Should().BeOfType<Reference<SalesInvoice>>()
            .Which.Value.Length.Should().BeInRange(1, 12);
    }

    [Fact]
    public void The_values_at_the_edges_and_past_them_come_from_the_declared_rules()
    {
        var sampler = new ValueObjectSampler(new Random(3));

        sampler.Boundaries<Quantity, int>().Should().Equal(1, 100);
        sampler.RejectedValues<Quantity, int>().Should().Equal(new SchemaViolation<int>(0, "Minimum (1)"), new(101, "Maximum (100)"));
        sampler.Boundaries<CountryCode, string>().Should().Equal("FR", "BE", "LU");
    }

    [Fact]
    public void A_type_whose_rules_refuse_every_candidate_names_the_registration_to_add()
    {
        var sampler = new ValueObjectSampler(new Random(4), new ValueObjectSamplerOptions { MaxAttempts = 1 });

        sampler.Invoking(sampler => sampler.Next<Iban, string>()).Should().Throw<ValueObjectSamplingException>()
            .WithMessage("*Register a generator of its underlying value: options.Use<Iban, string>(random => ...).");
    }

    [Fact]
    public void AutoFixture_creates_objects_whose_value_objects_their_rules_accept()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization(Options));

        for (var draw = 0; draw < 20; draw++)
        {
            var order = fixture.Create<Order>();
            var payment = fixture.Create<Payment>();

            Reference<PurchaseOrder>.TryCreate(order.Purchase.Value, out _).Should().BeTrue();
            order.Invoice.Should().NotBeNull();
            Reference<SalesInvoice>.TryCreate(order.Invoice!.Value.Value, out _).Should().BeTrue();
            order.Quantity.Value.Should().BeInRange(1, 100);
            Iban.TryCreate(payment.Account.Value, out _).Should().BeTrue();
            PaymentId.TryCreate(payment.Id.Value, out _).Should().BeTrue();
        }

        var once = new ValueObjectCustomization(new ValueObjectSamplerOptions { MaxAttempts = 1 }, new Random(4));
        new Fixture().Invoking(fixture => fixture.Customize(once).Create<Iban>()).Should()
            .Throw<ObjectCreationException>().Which.InnerException.Should().BeOfType<ValueObjectSamplingException>()
            .Which.Message.Should().EndWith("fixture.Register(() => Iban.Create(...)).");
    }

    [Fact]
    public void Bogus_gives_every_value_object_member_a_rule_that_strict_mode_accepts_and_the_seed_replays()
    {
        static List<string> Generate(int seed) => [.. new Faker<Payment>()
            .UseSeed(seed)
            .StrictMode(true)
            .RuleForValueObjects(Options)
            .Generate(10)
            .Select(payment => $"{payment.Id} {payment.Account} {payment.Amount}")];

        var payments = new Faker<Payment>().StrictMode(true).RuleForValueObjects(Options).Generate(20);

        payments.Should().AllSatisfy(payment =>
        {
            Iban.TryCreate(payment.Account.Value, out _).Should().BeTrue();
            Amount.TryCreate(payment.Amount.Value, out _).Should().BeTrue();
            PaymentId.TryCreate(payment.Id.Value, out _).Should().BeTrue();
        });
        Generate(7).Should().Equal(Generate(7));
        new Faker().ValueObject<Quantity>().Value.Should().BeInRange(1, 100);
    }

    [Fact]
    public void FsCheck_derives_the_objects_holding_value_objects_and_shrinks_to_values_their_rules_accept()
    {
        var map = ArbMap.Default
            .MergeValueObjects(typeof(Iban).Assembly, Options)
            .MergeValueObject<Reference<PurchaseOrder>, string>()
            .MergeValueObject<Reference<SalesInvoice>, string>();

        Prop.ForAll(map.ArbFor<Order>(), order => order.Quantity.Value is >= 1 and <= 100
                && Reference<PurchaseOrder>.TryCreate(order.Purchase.Value, out _))
            .Check(Config.QuickThrowOnFailure.WithQuietOnSuccess(true));
        Prop.ForAll(map.ArbFor<Payment>(), payment => Iban.TryCreate(payment.Account.Value, out _))
            .Check(Config.QuickThrowOnFailure.WithQuietOnSuccess(true));

        var falsified = () => Prop.ForAll(ValueObjectArbitrary.For<Quantity, int>(), quantity => quantity.Value < 50)
            .Check(Config.QuickThrowOnFailure.WithQuietOnSuccess(true));

        // FsCheck writes a "Shrunk:" line only when it shrank at least once: a first counterexample of 50 is "Original:" alone.
        var lines = falsified.Should().Throw<Exception>().Which.Message.ReplaceLineEndings("\n").Split('\n');
        var shrunk = Array.IndexOf(lines, "Shrunk:");
        lines[(shrunk >= 0 ? shrunk : Array.IndexOf(lines, "Original:")) + 1].Should().Be("50");
    }

    private static void Draws<TSelf, TValue>(ValueObjectSampler sampler)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var drawn = Enumerable.Range(0, 100).Select(_ => sampler.Next<TSelf, TValue>()).ToList();

        foreach (var value in drawn)
        {
            TSelf.TryCreate(value.Value, out var again).Should().BeTrue(typeof(TSelf).Name);
            again.Should().Be(value);
        }

        drawn.Distinct().Should().HaveCountGreaterThan(1, typeof(TSelf).Name);
    }

    private static string DrawIban(Random random)
    {
        const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var bban = string.Create(random.Next(11, 31), random, static (span, random) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Base36[random.Next(Base36.Length)];
            }
        });

        var remainder = 0;
        foreach (var character in bban + "FR00")
        {
            remainder = char.IsAsciiDigit(character)
                ? ((remainder * 10) + (character - '0')) % 97
                : ((remainder * 100) + (character - 'A' + 10)) % 97;
        }

        return "FR" + (98 - remainder).ToString("D2", CultureInfo.InvariantCulture) + bban;
    }
}
