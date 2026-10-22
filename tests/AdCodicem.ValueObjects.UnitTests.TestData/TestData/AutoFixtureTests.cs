using AdCodicem.ValueObjects.AutoFixture;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AutoFixture;
using AutoFixture.Kernel;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>
/// The AutoFixture customization: every value object a fixture creates, asked for or inside an object it builds, is one
/// its rules accept, and the fixture's own registrations still win.
/// </summary>
public class AutoFixtureTests
{
    [Fact]
    public void Every_value_object_of_an_object_the_fixture_builds_is_one_its_rules_accept()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization());

        for (var draw = 0; draw < 20; draw++)
        {
            var order = fixture.Create<TestDataOrder>();

            order.Backup.Should().NotBeNull();
            order.Contacts.Should().NotBeEmpty();
            order.Grades.Should().NotBeEmpty();
            order.Note.Should().NotBeNullOrEmpty("a member that is no value object stays AutoFixture's");
            order.ValueObjects().ShouldAllBeAccepted();
            order.Reserved.Value.Should().Be(0, "AutoFixture fills no private setter");
        }
    }

    [Fact]
    public void A_value_object_is_drawn_from_its_rules_asked_for_directly_or_as_its_nullable_type()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization());

        var ibans = Enumerable.Range(0, 50).Select(_ => fixture.Create<Iban>()).ToList();
        ibans.Should().AllSatisfy(iban => Iban.TryCreate(iban.Value, out _).Should().BeTrue());
        ibans.Distinct().Should().HaveCountGreaterThan(1);

        var backup = fixture.Create<Iban?>();
        backup.Should().NotBeNull();
        Iban.TryCreate(backup!.Value.Value, out _).Should().BeTrue();

        var quantities = Enumerable.Range(0, 200).Select(_ => fixture.Create<Quantity>().Value).ToList();
        quantities.Should().AllSatisfy(quantity => quantity.Should().BeInRange((short)0, (short)1000));
        quantities.Distinct().Should().HaveCountGreaterThan(50, "the bounds are not walked from 1 as AutoFixture walks its numbers");
    }

    [Fact]
    public void A_closed_set_is_drawn_from_every_known_value_and_a_construction_like_any_value_object()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization(new ValueObjectSamplerOptions(), new Random(4)));

        Enumerable.Range(0, 50).Select(_ => fixture.Create<CountryCode>()).Distinct().Should()
            .BeEquivalentTo([CountryCode.France, CountryCode.Belgium, CountryCode.Luxembourg]);
        fixture.Create<Reference<SalesInvoice>>().Value.Length.Should().BeInRange(1, 12);
        fixture.Create<BirthDate>().Value.Should().BeOnOrAfter(BirthDate.Minimum).And.BeOnOrBefore(BirthDate.Maximum);
    }

    [Fact]
    public void A_value_object_written_by_hand_is_resolved_and_drawn_from_its_schema()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization());

        var ledger = fixture.Create<TestDataLedger>();

        ledger.Tally.Value.Should().BeInRange(1, 9);
        ledger.Accounts.Should().NotBeEmpty("AutoFixture fills an array of nullable value objects through the builder");
        ledger.Accounts.Should().AllSatisfy(account => account.Should().NotBeNull());
        ValueObjectRegistry.TryGet(typeof(Declared<Tally, int>), out _).Should().BeTrue("the builder resolved it, and the registry keeps it");
    }

    [Fact]
    public void A_request_for_anything_but_a_value_object_is_left_to_AutoFixture()
    {
        var builder = new ValueObjectSpecimenBuilder(new ValueObjectSampler(new Random(1)));
        var context = Substitute.For<ISpecimenContext>();

        builder.Create(typeof(string), context).Should().BeOfType<NoSpecimen>();
        builder.Create(typeof(MarkerOnlyValue), context).Should().BeOfType<NoSpecimen>();
        builder.Create(typeof(Quantity).GetProperty(nameof(Quantity.Value))!, context).Should().BeOfType<NoSpecimen>();
        builder.Create(typeof(Quantity), context).Should().BeOfType<Quantity>();
        builder.Create(typeof(Quantity?), context).Should().BeOfType<Quantity>();
        ValueObjectRegistry.TryGet(typeof(MarkerOnlyValue), out _).Should().BeFalse("asking registered nothing");

        new Fixture().Customize(new ValueObjectCustomization()).Create<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_registration_of_the_fixture_wins_whether_it_comes_before_or_after_the_customization()
    {
        var before = new Fixture();
        before.Register(() => EvenCode.Create("EVEN-22"));
        before.Customize(new ValueObjectCustomization());

        var after = new Fixture().Customize(new ValueObjectCustomization());
        after.Register(() => EvenCode.Create("EVEN-44"));

        before.Create<EvenCode>().Value.Should().Be("EVEN-22");
        after.Create<EvenCode>().Value.Should().Be("EVEN-44");
    }

    [Fact]
    public void A_specimen_builder_the_fixture_held_before_the_customization_wins()
    {
        var fixture = new Fixture();
        fixture.Customizations.Add(new FilteringSpecimenBuilder(new FixedBuilder(Iban.Example), new ExactTypeSpecification(typeof(Iban))));
        fixture.Customize(new ValueObjectCustomization(new ValueObjectSamplerOptions(), new Random(6)));

        Enumerable.Range(0, 10).Select(_ => fixture.Create<Iban>()).Should().AllBeEquivalentTo(Iban.Example);
        fixture.Create<Quantity>().Value.Should().BeInRange((short)0, (short)1000, "the customization answers the rest");
    }

    [Fact]
    public void A_type_no_candidate_satisfies_fails_naming_the_registration_of_the_fixture()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization());

        var creation = () => fixture.Create<EvenCode>();

        var failure = creation.Should().Throw<ObjectCreationException>().Which.InnerException.Should()
            .BeOfType<ValueObjectSamplingException>().Which;
        failure.ValueObjectType.Should().Be<EvenCode>();
        failure.ErrorCode.Should().Be("checksum");
        failure.Attempts.Should().Be(100);
        failure.FromGenerator.Should().BeFalse();
        failure.Message.Should().EndWith("Register a generator of its underlying value: fixture.Register(() => EvenCode.Create(...)).");
    }

    [Fact]
    public void A_generator_registered_with_the_options_draws_and_keeps_the_sampler_s_message_when_its_values_are_refused()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization(new ValueObjectSamplerOptions().Use<EvenCode, string>(EvenCodes.Draw)));
        var refusing = new Fixture().Customize(new ValueObjectCustomization(new ValueObjectSamplerOptions().Use<EvenCode, string>(_ => "EVEN-1")));

        Enumerable.Range(0, 20).Select(_ => fixture.Create<EvenCode>()).Should()
            .AllSatisfy(code => EvenCodes.Validate(code.Value).IsValid.Should().BeTrue());
        var failure = refusing.Invoking(f => f.Create<EvenCode>()).Should().Throw<ObjectCreationException>().Which.InnerException.Should()
            .BeOfType<ValueObjectSamplingException>().Which;
        failure.FromGenerator.Should().BeTrue();
        failure.Message.Should().EndWith("Make the generator registered with options.Use<EvenCode, string>(random => ...) give values its rules accept.");
    }

    [Fact]
    public void A_seeded_random_creates_the_same_values_for_the_same_requests()
    {
        static List<string> Draw()
        {
            var fixture = new Fixture().Customize(new ValueObjectCustomization(new ValueObjectSamplerOptions(), new Random(5)));
            return [.. Enumerable.Range(0, 10).Select(_ => $"{fixture.Create<Iban>()} {fixture.Create<Quantity>()} {fixture.Create<CountryCode>()}")];
        }

        var first = Draw();

        first.Should().Equal(Draw());
        first.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Parallel_creations_over_the_shared_random_are_each_accepted()
    {
        var fixture = new Fixture().Customize(new ValueObjectCustomization());
        var refused = 0;

        Parallel.For(0, 2000, draw =>
        {
            if (!Iban.TryCreate(fixture.Create<Iban>().Value, out _))
            {
                Interlocked.Increment(ref refused);
            }
        });

        refused.Should().Be(0);
    }

    [Fact]
    public void The_customization_refuses_missing_arguments()
    {
        var withoutOptions = () => new ValueObjectCustomization(null!);
        var withoutRandom = () => new ValueObjectCustomization(new ValueObjectSamplerOptions(), null!);
        var withoutFixture = () => new ValueObjectCustomization().Customize(null!);

        withoutOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
        withoutRandom.Should().Throw<ArgumentNullException>().WithParameterName("random");
        withoutFixture.Should().Throw<ArgumentNullException>().WithParameterName("fixture");
    }
}
