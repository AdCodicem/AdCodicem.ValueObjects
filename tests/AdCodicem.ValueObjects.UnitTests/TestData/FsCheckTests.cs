using System.Globalization;
using System.Runtime.Loader;
using AdCodicem.ValueObjects.FsCheck;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;
using FsCheck;
using FsCheck.Fluent;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>
/// The FsCheck arbitraries: merged one per type so that the map derives the types holding value objects, biased towards the
/// declared edges, replayed from FsCheck's seed, and shrinking to values each type accepts.
/// </summary>
public class FsCheckTests
{
    /// <summary>Throws on the first counterexample, and stays silent when all of them pass.</summary>
    private static readonly Config Quick = Config.QuickThrowOnFailure.WithQuietOnSuccess(true);

    [Fact]
    public void A_map_merged_with_an_assembly_and_a_construction_derives_the_types_holding_them()
    {
        var map = ArbMap.Default.MergeValueObjects(typeof(Iban).Assembly).MergeValueObject<Reference<PurchaseOrder>, string>();

        var lines = map.ArbFor<TestDataLine>().Generator.Sample(100, new Rnd(11UL, 3UL), 10);

        lines.Should().AllSatisfy(line =>
        {
            Iban.TryCreate(line.Account.Value, out _).Should().BeTrue();
            if (line.Backup is { } backup)
            {
                Iban.TryCreate(backup.Value, out _).Should().BeTrue();
            }

            Quantity.TryCreate(line.Quantity.Value, out _).Should().BeTrue();
            CountryCode.TryCreate(line.Country.Value, out _).Should().BeTrue();
            line.Contacts.Should().AllSatisfy(contact => EmailAddress.TryCreate(contact.Value, out _).Should().BeTrue());
            line.Grades.Should().AllSatisfy(grade => Grade.TryCreate(grade.Value, out _).Should().BeTrue());
            Reference<PurchaseOrder>.TryCreate(line.PurchaseOrder.Value, out _).Should().BeTrue();
        });
        lines.Select(line => line.Account).Distinct().Should().HaveCountGreaterThan(1);
        map.ArbFor<string>().Generator.Sample(20, new Rnd(1UL, 1UL), 10).Should().Contain(text => text != null && text.Length > 0, "the other types stay FsCheck's");
    }

    /// <summary>
    /// A property may be the first code to need the assembly declaring the value objects, run alone through a filter: the
    /// merge runs the generated registration of the assembly itself. The fixture assembly is loaded into a context of its
    /// own, a copy nothing has used, whose value objects nothing has registered.
    /// </summary>
    [Fact]
    public void The_value_objects_of_a_module_nothing_has_used_yet_are_merged()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll");
        var assembly = new AssemblyLoadContext("A copy of the module nothing has used, for FsCheck").LoadFromAssemblyPath(path);
        var rank = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Rank", throwOnError: true)!;
        ValueObjectRegistry.TryGet(rank, out _).Should().BeFalse("nothing has used this copy of the assembly yet");

        var ranks = ArbMap.Default.MergeValueObjects(assembly).ArbFor(rank).Generator.Sample(50, new Rnd(3UL, 5UL), 10)
            .Select(drawn => drawn.Should().BeAssignableTo<IValueObject>().Which.GetBoxedValue().Should().BeOfType<int>().Which)
            .ToList();

        ranks.Should().AllSatisfy(value => value.Should().BeInRange(1, 10));
        ranks.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void A_construction_is_not_merged_with_its_assembly_and_FsCheck_names_it()
    {
        // Registered by now, as any construction another test resolved: the map does not depend on what ran before.
        ValueObjectRegistry.TryResolve(typeof(Reference<PurchaseOrder>), out _).Should().BeTrue();
        var map = ArbMap.Default.MergeValueObjects(typeof(Iban).Assembly);

        var derivation = () => map.ArbFor<TestDataLine>().Generator.Sample(1);

        derivation.Should().Throw<Exception>().Which.GetBaseException().Message.Should().Match("*Reference`1*not handled automatically*");
    }

    [Fact]
    public void Only_the_value_objects_of_the_assembly_given_are_merged()
    {
        ValueObjectRegistry.TryGet(typeof(Iban), out _).Should().BeTrue("the module initializer registered it");
        var map = ArbMap.Default.MergeValueObjects(typeof(string).Assembly);

        var derivation = () => map.ArbFor<Iban>().Generator.Sample(1);

        derivation.Should().Throw<Exception>().Which.GetBaseException().Message.Should().Match("*Iban*not handled automatically*");
    }

    [Fact]
    public void The_options_reach_every_arbitrary_merged()
    {
        var options = new ValueObjectSamplerOptions().Use<EvenCode, string>(EvenCodes.Draw);

        var merged = ArbMap.Default.MergeValueObjects(typeof(EvenCode).Assembly, options).GeneratorFor<EvenCode>().Sample(20);
        var mergedOne = ArbMap.Default.MergeValueObject<EvenCode, string>(options).GeneratorFor<EvenCode>().Sample(20);

        merged.Concat(mergedOne).Should().AllSatisfy(code => EvenCodes.Validate(code.Value).IsValid.Should().BeTrue());
    }

    [Fact]
    public void One_value_in_four_is_a_declared_boundary()
    {
        var quantities = ValueObjectArbitrary.For<Quantity, short>().Generator.Sample(2000, new Rnd(5UL, 9UL), 10);

        var atBoundaries = quantities.Count(quantity => quantity.Value is 0 or 1000);

        // A quarter of 2,000 is 500, give or take 19; a third would be 667 and a fifth 400. A uniform draw over 0..1000
        // lands on its edges twice in 1,001 draws.
        atBoundaries.Should().BeInRange(440, 570);
        quantities.Select(quantity => quantity.Value).Distinct().Should().HaveCountGreaterThan(100);
        ValueObjectArbitrary.For<CustomerId, Guid>().Generator.Sample(50, new Rnd(5UL, 9UL), 10).Distinct().Should()
            .HaveCount(50, "a type that declares no boundary is drawn alone");
    }

    [Fact]
    public void The_same_seed_replays_the_same_values()
    {
        var map = ArbMap.Default.MergeValueObjects(typeof(Iban).Assembly);

        var first = map.GeneratorFor<Iban>().Sample(10, new Rnd(42UL, 7UL), 10);

        first.Should().Equal(map.GeneratorFor<Iban>().Sample(10, new Rnd(42UL, 7UL), 10));
        first.Should().NotEqual(map.GeneratorFor<Iban>().Sample(10, new Rnd(43UL, 7UL), 10));
    }

    [Fact]
    public void Two_arbitraries_of_one_type_replay_the_same_values_from_the_same_seed()
    {
        var first = ValueObjectArbitrary.For<Label, string>().Generator.Sample(40, new Rnd(42UL, 7UL), 10);
        var second = ValueObjectArbitrary.For<Label, string>().Generator.Sample(40, new Rnd(42UL, 7UL), 10);

        first.Should().Equal(second, "the boundaries, a label of the declared length among them, are drawn from a fixed seed too");
        first.Should().Contain(label => label.Value.Length == 200);
    }

    [Fact]
    public void A_counterexample_shrinks_to_the_declared_values_through_the_underlying_shrinker()
    {
        var quantities = ValueObjectArbitrary.For<Quantity, short>();

        var check = () => Prop.ForAll(quantities, quantity => quantity.Value < 50).Check(Quick);

        Shrunk(check).Should().Be("50");
    }

    [Fact]
    public void A_value_object_over_a_type_FsCheck_does_not_shrink_shrinks_to_its_declared_values_alone()
    {
        var shrinker = ValueObjectArbitrary.For<BirthDate, DateOnly>();

        var check = () => Prop.ForAll(shrinker, date => date.Value.Year > 2050).Check(Quick);

        Shrunk(check).Should().Be(BirthDate.Create(BirthDate.Minimum).ToString());
        shrinker.Shrinker(BirthDate.Create(new DateOnly(2000, 6, 1))).Should().Equal(BirthDate.Create(BirthDate.Minimum));
    }

    [Theory]
    [InlineData("int")]
    [InlineData("string")]
    [InlineData("decimal")]
    [InlineData("DateTime")]
    public void A_value_object_over_a_type_FsCheck_shrinks_shrinks_through_its_shrinker_too(string underlying)
    {
        // A DateTime of UTC, as the sampler draws one, which FsCheck's shrinker would only make of no kind.
        var (shrinks, declared) = underlying switch
        {
            "int" => Shrinks<PageNumber, int>(PageNumber.Create(7)),
            "string" => Shrinks<Label, string>(Label.Create("draft")),
            "decimal" => Shrinks<Amount, decimal>(Amount.Create(437.25m)),
            _ => Shrinks<RecordedAt, DateTime>(RecordedAt.Create(new DateTime(2050, 6, 15, 13, 45, 12, 345, DateTimeKind.Utc))),
        };

        shrinks.Should().StartWith(declared).And.HaveCountGreaterThan(declared.Count, "FsCheck shrinks {0}", underlying);
    }

    [Fact]
    public void A_DateTime_drawn_in_UTC_shrinks_through_FsCheck_and_stays_in_UTC()
    {
        var recorded = RecordedAt.Create(new DateTime(2050, 6, 15, 13, 45, 12, 345, DateTimeKind.Utc));

        var shrinks = ValueObjectArbitrary.For<RecordedAt, DateTime>().Shrinker(recorded).Where(shrunk => shrunk.Value.Year == 2050);

        shrinks.Should().Equal([RecordedAt.Create(new DateTime(2050, 6, 15, 13, 45, 12))], "FsCheck drops the milliseconds first")
            .And.AllSatisfy(shrunk => shrunk.Value.Kind.Should().Be(DateTimeKind.Utc));
    }

    [Fact]
    public void A_normalizer_that_maps_a_shrink_back_does_not_send_the_shrink_round_in_circles()
    {
        var grades = ValueObjectArbitrary.For<Grade, char>();
        var codes = ValueObjectArbitrary.For<CurrencyCode, string>();

        var gradeCheck = () => Prop.ForAll(grades, grade => grade.Value is not ('B' or 'C')).Check(Quick);
        var codeCheck = () => Prop.ForAll(codes, code => code == CurrencyCode.Euro || code == CurrencyCode.UsDollar).Check(Quick);

        // FsCheck shrinks 'C' to 'a' and 'b', and 'B' to 'a', 'b' and 'c', which Grade upper-cases: B and C would shrink to
        // each other until FsCheck stopped, 2,500 shrinks later; CurrencyCode's AWE and BWE alike.
        grades.Shrinker(Grade.Create('C')).Should().Equal(Grade.Create('A'));
        ShrinkCount(gradeCheck).Should().BeLessThan(10);
        ShrinkCount(codeCheck).Should().BeLessThan(10);
    }

    [Fact]
    public void A_shrink_stops_at_the_example_once_it_is_reached()
    {
        var amounts = ValueObjectArbitrary.For<Amount, decimal>();

        var check = () => Prop.ForAll(amounts, amount => amount.Value < 100m).Check(Quick);

        Shrunk(check).Should().Be(Amount.Example.ToString());
    }

    [Fact]
    public void An_arbitrary_reads_nothing_of_its_type_until_it_draws()
    {
        var arbitrary = ValueObjectArbitrary.For<Declared<WatchedTally, int>, int>();
        var map = ArbMap.Default.MergeValueObject<Declared<WatchedTally, int>, int>();

        WatchedTally.Reads.Should().Be(0);

        map.GeneratorFor<Declared<WatchedTally, int>>().Sample(20).Should().AllSatisfy(tally => tally.Value.Should().BeInRange(1, 9));
        arbitrary.Generator.Sample(1).Should().ContainSingle();
        WatchedTally.Reads.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_draw_no_candidate_satisfies_fails_naming_the_options_to_give()
    {
        var withoutGenerator = () => ValueObjectArbitrary.For<EvenCode, string>().Generator.Sample(1);
        var refusingGenerator = () => ValueObjectArbitrary.For<EvenCode, string>(new ValueObjectSamplerOptions().Use<EvenCode, string>(_ => "EVEN-1"))
            .Generator.Sample(1);

        withoutGenerator.Should().Throw<ValueObjectSamplingException>().Which.Message.Should().EndWith(
            "Register a generator of its underlying value: options.Use<EvenCode, string>(random => ...), on the options given to "
            + "MergeValueObjects or ValueObjectArbitrary.For.");
        refusingGenerator.Should().Throw<ValueObjectSamplingException>().Which.Message.Should()
            .EndWith("Make the generator registered with options.Use<EvenCode, string>(random => ...) give values its rules accept.");
        ValueObjectArbitrary.For<EvenCode, string>(new ValueObjectSamplerOptions().Use<EvenCode, string>(EvenCodes.Draw))
            .Generator.Sample(20).Should().AllSatisfy(code => EvenCodes.Validate(code.Value).IsValid.Should().BeTrue());
    }

    [Fact]
    public void The_extensions_refuse_missing_arguments()
    {
        var withoutMap = () => ((IArbMap)null!).MergeValueObjects(typeof(Iban).Assembly);
        var withoutAssembly = () => ArbMap.Default.MergeValueObjects(null!);
        var withoutMapForOne = () => ((IArbMap)null!).MergeValueObject<Quantity, short>();

        withoutMap.Should().Throw<ArgumentNullException>().WithParameterName("map");
        withoutAssembly.Should().Throw<ArgumentNullException>().WithParameterName("assembly");
        withoutMapForOne.Should().Throw<ArgumentNullException>().WithParameterName("map");
    }

    /// <summary>Runs a check expected to fail, and reads the counterexample it shrank to off its message.</summary>
    /// <remarks>
    /// FsCheck writes a <c>Shrunk:</c> line only when it shrank the counterexample at least once: a first counterexample
    /// already as simple as it gets, a declared value drawn as a boundary for one, is the <c>Original:</c> line alone.
    /// </remarks>
    private static string Shrunk(Action check)
    {
        var lines = check.Should().Throw<Exception>().Which.Message.ReplaceLineEndings("\n").Split('\n');
        var shrunk = Array.IndexOf(lines, "Shrunk:");

        return lines[(shrunk >= 0 ? shrunk : Array.IndexOf(lines, "Original:")) + 1];
    }

    /// <summary>Runs a check expected to fail, and reads how many shrinks FsCheck made off its message.</summary>
    private static int ShrinkCount(Action check)
    {
        // "Falsifiable, after 3 tests (2500 shrinks) (seed)", or "(1 shrink)".
        var message = check.Should().Throw<Exception>().Which.Message;
        var start = message.IndexOf(" (", StringComparison.Ordinal) + 2;

        return int.Parse(message[start..message.IndexOf(" shrink", start, StringComparison.Ordinal)], CultureInfo.InvariantCulture);
    }

    /// <summary>The shrinks of a value through its arbitrary, and those of its declared values alone.</summary>
    private static (List<object> Shrinks, List<object> Declared) Shrinks<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => ([.. ValueObjectArbitrary.For<TSelf, TValue>().Shrinker(value).Cast<object>()],
            [.. ValueObjectSampler.Shrink<TSelf, TValue>(value).Cast<object>()]);
}
