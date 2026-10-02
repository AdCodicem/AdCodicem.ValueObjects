using System.Globalization;
using System.Reflection;
using AdCodicem.ValueObjects.Fixtures.WithoutGenerator;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// <see cref="ValueObjectRegistry.TryResolve"/>, the lookup that falls back to reflection for what did not
/// register itself.
/// </summary>
public class RegistryResolutionTests
{
    [Fact]
    public void A_registered_value_object_resolves_to_its_registration_nullable_or_not()
    {
        ValueObjectRegistry.TryGet(typeof(Iban), out var registered).Should().BeTrue();

        ValueObjectRegistry.TryResolve(typeof(Iban?), out var resolved).Should().BeTrue();

        resolved.Should().BeSameAs(registered);
    }

    [Fact]
    public void A_type_that_is_not_a_value_object_resolves_to_none()
    {
        ValueObjectRegistry.TryResolve(typeof(string), out var descriptor).Should().BeFalse();

        descriptor.Should().BeNull();
    }

    /// <summary>
    /// The fallback the registry documents for value objects written by hand: nothing registered them, so they are
    /// described by reflection, once, and the description is kept. Two of them, because the fallback caches the
    /// delegate that builds a description on its first use.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_is_described_by_reflection_and_kept()
    {
        ValueObjectRegistry.TryResolve(typeof(HandWrittenCounter), out var counter).Should().BeTrue();
        ValueObjectRegistry.TryResolve(typeof(HandWrittenCode?), out var code).Should().BeTrue();

        counter!.ValueObjectType.Should().Be<HandWrittenCounter>();
        counter.ValueType.Should().Be<int>();
        counter.Schema.Should().BeSameAs(ValueObjectSchema.Unconstrained, "a type without an annotation declares no rule");
        counter.Create(5).Should().Be(HandWrittenCounter.Create(5));

        code!.ValueType.Should().Be<string>();
        code.Create(" abc ").Should().Be(HandWrittenCode.Create("ABC"));

        ValueObjectRegistry.TryGet(typeof(HandWrittenCounter), out var kept).Should().BeTrue();
        kept.Should().BeSameAs(counter);
    }

    /// <summary>
    /// A module initializer runs once its assembly is first used, and code that only reflects over the types of an
    /// assembly - building a model, scanning for handlers - may not have used it yet. The registry then forces the
    /// registration the generator emitted rather than describing the type again by reflection. The fixture assembly
    /// is loaded and searched by name, and nothing else in the suite uses it, so its initializer has not run when
    /// the test starts.
    /// </summary>
    [Fact]
    public void A_value_object_whose_module_has_not_run_yet_is_registered_on_first_resolution()
    {
        var assembly = Assembly.Load("AdCodicem.ValueObjects.Fixtures.Untouched");
        var rank = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Rank", throwOnError: true)!;
        ValueObjectRegistry.TryGet(rank, out _).Should().BeFalse("nothing has used its assembly yet");

        ValueObjectRegistry.TryResolve(rank, out var descriptor).Should().BeTrue();

        // Only the generated registration falls back to the XML summary for a description.
        descriptor!.Schema.Description.Should().Be("A rank, from 1 to 10.");
        ValueObjectRegistry.TryGet(rank, out var registered).Should().BeTrue();
        registered.Should().BeSameAs(descriptor);
    }

    /// <summary>
    /// Where the generator does not run, an annotated value object written by hand registers nothing, and the
    /// registry reads its rules back from the annotation.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_annotated_where_no_generator_runs_is_described_from_its_annotation()
    {
        ValueObjectRegistry.TryResolve(typeof(LightColor), out var color).Should().BeTrue();
        ValueObjectRegistry.TryResolve(typeof(FloorNumber), out var floor).Should().BeTrue();

        color!.Schema.IsClosedValueSet.Should().BeTrue();
        color.Schema.KnownValues.Should().Equal("red", "green");
        color.Schema.MinLength.Should().BeNull();
        color.Schema.MaxLength.Should().Be(6);
        color.Schema.Pattern.Should().Be("^[a-z]+$");
        color.Schema.Format.Should().Be("color");
        color.Schema.Example.Should().Be("red");
        color.Schema.Description.Should().Be("The color of a traffic light.");
        color.Create("red").Should().BeSameAs(color.Create("red"), "the members of a closed set are boxed once");

        floor!.ValueType.Should().Be<int>();
        floor.Schema.Minimum.Should().Be("1");
        floor.Schema.Maximum.Should().Be("10");
        floor.Schema.Pattern.Should().BeNull();
        floor.Schema.MaxLength.Should().BeNull();
        floor.Schema.IsClosedValueSet.Should().BeFalse();
        floor.Schema.KnownValues.Should().BeEmpty();
    }

    /// <summary>
    /// The generated schema publishes each known value as the type holds it, normalized. Read back from an
    /// annotation, a known value goes through the type the same way: normalized when it is of the underlying type,
    /// parsed when it is text for a type no attribute argument can carry, and left as written when the type cannot
    /// parse it, which nothing checked where no generator runs.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_publishes_its_known_values_as_the_type_holds_them()
    {
        ValueObjectRegistry.TryResolve(typeof(ShirtSize), out var size).Should().BeTrue();
        ValueObjectRegistry.TryResolve(typeof(SalesTaxRate), out var rate).Should().BeTrue();

        size!.Schema.KnownValues.Should().Equal("S", "M");
        size.Create("s").Should().BeSameAs(size.Create("S"), "the shared box is found under the normalized value");

        rate!.Schema.KnownValues.Should().Equal(20.0m, 5.5m, "twenty");
        rate.Schema.KnownValues[1].Should().BeOfType<decimal>().Which.ToString(CultureInfo.InvariantCulture)
            .Should().Be("5.5", "the declared 5.50 is normalized to one decimal");
    }

    /// <summary>
    /// A pattern declared through <see cref="IValueObjectPatternValidator"/> is an interface the type implements,
    /// read through it whether the type carries an annotation, whose other rules are read too, or none at all.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_publishes_the_pattern_of_its_hook()
    {
        ValueObjectRegistry.TryResolve(typeof(PostalCode), out var postal).Should().BeTrue();
        ValueObjectRegistry.TryResolve(typeof(DepartmentCode), out var department).Should().BeTrue();

        postal!.Schema.Pattern.Should().Be("^[0-9]{5}$");
        postal.Schema.MaxLength.Should().Be(5, "the annotation is still read beside the hook");

        department!.Schema.Pattern.Should().Be("^[0-9]{2}$");
        department.Schema.MaxLength.Should().BeNull();
        department.TryParse("7A", null, out _, out var rejected).Should().BeFalse();
        rejected.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    [Fact]
    public void A_value_object_nothing_registered_still_reports_its_underlying_type()
    {
        ValueObjectRegistry.GetUnderlyingType(typeof(HandWrittenCounter)).Should().Be<int>();
        ValueObjectRegistry.GetUnderlyingType(typeof(UnregisteredCode?)).Should().Be<string>();

        ValueObjectRegistry.TryGet(typeof(UnregisteredCode), out _).Should().BeFalse("answering registers nothing");
    }

    [Fact]
    public void Scanning_an_assembly_that_declares_no_value_object_is_harmless_and_done_once()
    {
        var scan = () => ValueObjectRegistry.EnsureAssemblyRegistered(typeof(object).Assembly);

        scan.Should().NotThrow();
        scan.Should().NotThrow("the second scan of an assembly returns at once");
    }

    /// <summary>
    /// Each of these implements the marker, and none is a struct implementing
    /// <see cref="IValueObject{TSelf, TValue}"/> over itself, which is what a descriptor is built from. So none is a
    /// value object: it has no underlying type, even where it declares a value through
    /// <see cref="IValueObject{TValue}"/>, and a Try method answers with <see langword="false"/>, not with an
    /// exception.
    /// </summary>
    [Theory]
    [InlineData(typeof(IValueObject))]
    [InlineData(typeof(IValueObject<string>))]
    [InlineData(typeof(IEntityId))]
    [InlineData(typeof(MarkerOnlyValue))]
    [InlineData(typeof(ClassBackedValue))]
    [InlineData(typeof(SelflessValue))]
    [InlineData(typeof(SelflessValue?))]
    public void A_type_no_descriptor_can_describe_is_no_value_object_and_resolves_to_none(Type type)
    {
        ValueObjectRegistry.IsValueObject(type).Should().BeFalse();
        ValueObjectRegistry.GetUnderlyingType(type).Should().BeNull();

        ValueObjectRegistry.TryResolve(type, out var descriptor).Should().BeFalse();
        descriptor.Should().BeNull();
    }

    /// <summary>
    /// A value object is what the registry can describe, whether it registered itself or not: a hand-written one that
    /// nothing has resolved is answered from its interfaces, without registering it.
    /// </summary>
    [Theory]
    [InlineData(typeof(Iban))]
    [InlineData(typeof(Iban?))]
    [InlineData(typeof(UnregisteredCode))]
    [InlineData(typeof(UnregisteredCode?))]
    public void A_struct_implementing_the_contract_over_itself_is_a_value_object(Type type)
    {
        ValueObjectRegistry.IsValueObject(type).Should().BeTrue();

        ValueObjectRegistry.TryGet(typeof(UnregisteredCode), out _).Should().BeFalse("answering registers nothing");
    }
}
