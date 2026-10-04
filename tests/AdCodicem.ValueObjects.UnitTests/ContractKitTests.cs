using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Xunit.Sdk;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The contract kit on what the contracts of this assembly never meet: a value object it cannot fully check, one written
/// by hand whose schema is out of step with itself, and one from a module nothing has used yet.
/// </summary>
public partial class ContractKitTests
{
    /// <summary>
    /// The declared limits live in the registry's schema. A type that did not register itself has none to read,
    /// and a check that cannot run must say so rather than pass.
    /// </summary>
    [Fact]
    public void The_length_check_reports_itself_skipped_for_a_type_the_registry_does_not_know()
    {
        ValueObjectRegistry.TryGet(typeof(UnregisteredCode), out _).Should().BeFalse();

        var act = () => new UnregisteredContract().Declared_length_limits_hold_for_every_accepted_value();

        act.Should().Throw<SkipException>().WithMessage("*'UnregisteredCode' did not register itself*");
    }

    /// <summary>
    /// A consumer's contracts usually live in a test assembly of their own, so a registry check may be the first test
    /// to need the assembly declaring the type - run alone, through a filter. The kit does not count on the module
    /// initializer of that assembly having run: it runs the generated registration itself.
    /// </summary>
    [Fact]
    public void The_registry_checks_find_a_type_from_a_module_nothing_has_used_yet()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll");
        var assembly = new AssemblyLoadContext("A copy of the module nothing has used").LoadFromAssemblyPath(path);
        var rank = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Rank", throwOnError: true)!;
        ValueObjectRegistry.TryGet(rank, out _).Should().BeFalse("nothing has used this copy of the assembly yet");

        var contract = (IRegistryChecks)Activator.CreateInstance(typeof(RankContract<>).MakeGenericType(rank))!;
        contract.The_type_is_discoverable_at_run_time();
        contract.Declared_length_limits_hold_for_every_accepted_value();

        ValueObjectRegistry.TryGet(rank, out var descriptor).Should().BeTrue();
        descriptor!.Schema.Description.Should().Be("A rank, from 1 to 10.", "the generated registration describes it");
    }

    /// <summary>
    /// A construction of a generic value object is never registered as such, and in a module nothing has used yet,
    /// nothing resolved it either: the kit resolves it before checking it is discoverable.
    /// </summary>
    [Fact]
    public void The_registry_checks_find_a_construction_from_a_module_nothing_has_used_yet()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll");
        var assembly = new AssemblyLoadContext("Another copy of the module nothing has used").LoadFromAssemblyPath(path);
        var standing = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Standing`1", throwOnError: true)!
            .MakeGenericType(typeof(string));
        ValueObjectRegistry.TryGet(standing, out _).Should().BeFalse("nothing has used this copy of the assembly yet");

        var contract = (IRegistryChecks)Activator.CreateInstance(typeof(RankContract<>).MakeGenericType(standing))!;
        contract.The_type_is_discoverable_at_run_time();
        contract.Declared_length_limits_hold_for_every_accepted_value();

        ValueObjectRegistry.TryGet(standing, out var descriptor).Should().BeTrue();
        descriptor!.Schema.Minimum.Should().Be("1", "the generated schema describes the construction");
    }

    /// <summary>
    /// The declared example and known values are read off the schema the type declares, as generic code reads it, which
    /// takes no registration: a value object written by hand that nothing registered is checked as any other, and one
    /// that declares neither has nothing to check.
    /// </summary>
    [Fact]
    public void The_declaration_checks_read_the_schema_of_a_type_the_registry_does_not_know()
    {
        var unregistered = new UnregisteredContract();

        new FanSpeedContract<LinedUp>().The_declared_example_is_accepted();
        new FanSpeedContract<LinedUp>().Every_declared_known_value_is_accepted();
        unregistered.Invoking(checks => checks.The_declared_example_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'UnregisteredCode' declares no example.");
        unregistered.Invoking(checks => checks.Every_declared_known_value_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'UnregisteredCode' declares no known value.");
        ValueObjectRegistry.TryGet(typeof(FanSpeed<LinedUp>), out _).Should().BeFalse("nothing registers a value object written by hand");
        ValueObjectRegistry.TryGet(typeof(UnregisteredCode), out _).Should().BeFalse("nothing registers a value object written by hand");
    }

    /// <summary>
    /// A value object written by hand initializes when first used, so its example and its known values reach nothing
    /// before the kit: the kit is what reports one its type refuses, with the rule it broke, and a known value that is
    /// not of the underlying type, as a schema written by hand may hold, with the type it is of.
    /// </summary>
    /// <param name="declared">The schema the value object written by hand declares.</param>
    /// <param name="check">The check of the kit that reports it.</param>
    /// <param name="failure">What the kit reports.</param>
    [Theory]
    [MemberData(nameof(DeclarationsRefused))]
    public void A_declaration_a_schema_written_by_hand_holds_and_its_type_refuses_fails_the_contract(
        Type declared,
        string check,
        string failure)
    {
        var speed = typeof(FanSpeed<>).MakeGenericType(declared);
        var contract = (IDeclarationChecks)Activator.CreateInstance(typeof(FanSpeedContract<>).MakeGenericType(declared))!;
        Action act = check == "example" ? contract.The_declared_example_is_accepted : contract.Every_declared_known_value_is_accepted;

        act.Should().Throw<XunitException>().WithMessage(failure);
        ValueObjectRegistry.TryGet(speed, out _).Should().BeFalse("nothing registers a value object written by hand");
    }

    /// <summary>Schemas written by hand declaring what their type refuses, and what the kit reports on each.</summary>
    public static TheoryData<Type, string, string> DeclarationsRefused => new()
    {
        {
            typeof(Overreaching),
            "example",
            "The example '3' declared on 'FanSpeed`1' is refused (value_object.out_of_range): A fan runs low or high."
        },
        {
            typeof(Overreaching),
            "known value",
            "The known value '3' declared on 'FanSpeed`1' is refused (value_object.out_of_range): A fan runs low or high."
        },
        {
            typeof(Unknown),
            "known value",
            "The known value null declared on 'FanSpeed`1' is not of its underlying type, Int32."
        },
        {
            typeof(Mistyped),
            "known value",
            "The known value '2' (Int64) declared on 'FanSpeed`1' is not of its underlying type, Int32."
        },
    };

    /// <summary>
    /// Known values left at the default of their array, as an init accessor allows, are none, rather than an exception
    /// the kit throws; details beside them still have no value to line up with.
    /// </summary>
    [Fact]
    public void Known_values_left_at_their_default_are_none()
    {
        new FanSpeedContract<Blank>().Invoking(checks => checks.Every_declared_known_value_is_accepted())
            .Should().Throw<SkipException>().WithMessage("*'FanSpeed`1' declares no known value.");
        new FanSpeedContract<Blank>().Invoking(checks => checks.The_known_value_details_line_up_with_the_known_values())
            .Should().Throw<SkipException>().WithMessage("*'FanSpeed`1' declares no known value.");
        new FanSpeedContract<Unlisted>().Invoking(checks => checks.Every_declared_known_value_is_accepted())
            .Should().Throw<SkipException>().WithMessage("*'FanSpeed`1' declares no known value.");
    }

    /// <summary>
    /// The OpenAPI document publishes the example as it is declared. A pattern runs only at run time, so the generator
    /// lets this one through, and the kit is what catches it, with the rule it broke.
    /// </summary>
    [Fact]
    public void An_example_its_type_refuses_fails_the_contract_with_the_rule_it_broke()
    {
        var act = () => new DigitsContract().The_declared_example_is_accepted();

        act.Should().Throw<XunitException>().WithMessage(
            "The example 'abc' declared on 'Digits' is refused (value_object.invalid_format): The value does not match the expected format.*");
    }

    /// <summary>
    /// A refused known value throws from the type initializer, and every check of the type fails with a
    /// <see cref="TypeInitializationException"/>. A construction of a generic value object initializes when first used,
    /// which the kit does itself, so it reports the rule behind the initializer, as often as it is asked.
    /// </summary>
    [Fact]
    public void A_known_value_its_type_refuses_fails_the_contract_with_the_rule_it_broke()
    {
        var contract = new ShadeContract();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            contract.Invoking(checks => checks.Every_declared_known_value_is_accepted()).Should().Throw<XunitException>()
                .WithMessage(
                    "'Shade' cannot initialize: a value it creates as it does, a known value most likely, is refused "
                    + "(value_object.invalid_format): 'Shade`1' rejected the supplied value: The shade is not lower case.");
            contract.Invoking(checks => checks.The_declared_example_is_accepted()).Should().Throw<XunitException>()
                .WithMessage("'Shade' cannot initialize*");
            contract.Invoking(checks => checks.The_known_value_details_line_up_with_the_known_values())
                .Should().Throw<XunitException>().WithMessage("'Shade' cannot initialize*");
        }
    }

    /// <summary>
    /// A type that fails to initialize for any other reason than a refused value is no matter of the kit's: the runner
    /// reports the exception as it is.
    /// </summary>
    [Fact]
    public void A_type_failing_to_initialize_for_another_reason_is_left_to_the_runner()
    {
        var act = () => new BrokenContract().Every_declared_known_value_is_accepted();

        act.Should().Throw<TypeInitializationException>().WithInnerException<InvalidOperationException>();
    }

    /// <summary>
    /// A type whose example and known values it accepts passes; one that declares neither has nothing to check.
    /// </summary>
    [Fact]
    public void The_declaration_checks_pass_on_what_a_type_accepts_and_skip_what_it_does_not_declare()
    {
        var declaring = new DigitsContract();
        var silent = new SilentContract();

        declaring.Every_declared_known_value_is_accepted();
        silent.Invoking(checks => checks.The_declared_example_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'Silent' declares no example.");
        silent.Invoking(checks => checks.Every_declared_known_value_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'Silent' declares no known value.");
    }

    /// <summary>
    /// The details of the known values name the members of the enumeration a client generated from the OpenAPI document
    /// holds. A schema written by hand may list them out of step with the known values, and the kit says where they part,
    /// reading the schema the type declares, which takes no registration.
    /// </summary>
    /// <param name="declared">The schema the value object written by hand declares.</param>
    /// <param name="failure">What the kit reports.</param>
    [Theory]
    [MemberData(nameof(DetailsOutOfStep))]
    public void Known_value_details_out_of_step_with_the_known_values_fail_the_contract_with_where_they_part(
        Type declared,
        string failure)
    {
        var speed = typeof(FanSpeed<>).MakeGenericType(declared);
        var contract = (IDeclarationChecks)Activator.CreateInstance(typeof(FanSpeedContract<>).MakeGenericType(declared))!;

        contract.Invoking(checks => checks.The_known_value_details_line_up_with_the_known_values())
            .Should().Throw<XunitException>().WithMessage(failure);
        ValueObjectRegistry.TryGet(speed, out _).Should().BeFalse("nothing registers a value object written by hand");
    }

    /// <summary>Schemas written by hand whose known value details are out of step, and what the kit reports on each.</summary>
    public static TheoryData<Type, string> DetailsOutOfStep => new()
    {
        {
            typeof(Undetailed),
            "'FanSpeed`1' has 0 known value details for 2 known values: KnownValueDetails lists the values of KnownValues "
            + "one for one, in the same order."
        },
        {
            typeof(OneShort),
            "'FanSpeed`1' has 1 known value details for 2 known values: KnownValueDetails lists the values of KnownValues "
            + "one for one, in the same order."
        },
        {
            typeof(Unlisted),
            "'FanSpeed`1' has 1 known value details for 0 known values: KnownValueDetails lists the values of KnownValues "
            + "one for one, in the same order."
        },
        {
            typeof(Swapped),
            "The known value detail 'High' of 'FanSpeed`1' is of the value '2', where the known value at index 0 is '1': "
            + "KnownValueDetails lists the values of KnownValues one for one, in the same order."
        },
        {
            typeof(Widened),
            "The known value detail 'Low' of 'FanSpeed`1' is of the value '1' (Int64), where the known value at index 0 is "
            + "'1' (Int32): KnownValueDetails lists the values of KnownValues one for one, in the same order."
        },
        {
            typeof(Unknown),
            "The known value detail 'High' of 'FanSpeed`1' is of the value '2', where the known value at index 1 is null: "
            + "KnownValueDetails lists the values of KnownValues one for one, in the same order."
        },
        {
            typeof(Missing),
            "The known value detail at index 1 of 'FanSpeed`1' has no name, which the OpenAPI document names the value after."
        },
        {
            typeof(Nameless),
            "The known value detail at index 0 of 'FanSpeed`1' has no name, which the OpenAPI document names the value after."
        },
    };

    /// <summary>
    /// Details that line up pass, on a value object written by hand as on a generated one. A type that declares no known
    /// value has nothing to check.
    /// </summary>
    [Fact]
    public void The_detail_check_passes_on_details_that_line_up_and_skips_a_type_that_declares_no_known_value()
    {
        new FanSpeedContract<LinedUp>().The_known_value_details_line_up_with_the_known_values();
        new DigitsContract().The_known_value_details_line_up_with_the_known_values();

        new SilentContract().Invoking(checks => checks.The_known_value_details_line_up_with_the_known_values())
            .Should().Throw<SkipException>().WithMessage("*'Silent' declares no known value.");
    }

    /// <summary>A code of digits, whose example is not one, and whose known value is.</summary>
    [ValueObject<string>(Example = "abc")]
    [KnownValue("Zero", "0")]
    public readonly partial struct Digits : IValueObjectPatternValidator
    {
        [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        public static partial Regex Pattern { get; }
    }

    /// <summary>A value object that declares neither an example nor a known value.</summary>
    [ValueObject<string>]
    public readonly partial struct Silent;

    /// <summary>Private, so the runner does not discover it: its example is wrong on purpose.</summary>
    private sealed class DigitsContract : ValueObjectContract<Digits, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["0", "42"];

        protected override IEnumerable<string> RejectedValues => ["abc"];
    }

    /// <summary>Private, so the runner does not discover it: the checks it is used for skip.</summary>
    private sealed class SilentContract : ValueObjectContract<Silent, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["a", "b"];

        protected override IEnumerable<string> RejectedValues => [string.Empty];
    }

    /// <summary>
    /// Private, so the runner does not discover it: its type cannot initialize, on purpose.
    /// </summary>
    private sealed class ShadeContract : ValueObjectContract<Shade<ContractKitTests>, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["light", "pale"];

        protected override IEnumerable<string> RejectedValues => ["DARK"];
    }

    /// <summary>
    /// Private, so the runner does not discover it: its type cannot initialize, on purpose.
    /// </summary>
    private sealed class BrokenContract : ValueObjectContract<Broken<ContractKitTests>, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["a", "b"];

        protected override IEnumerable<string> RejectedValues => [string.Empty];
    }

    /// <summary>
    /// A shade of some surface, written in lower case, one of whose known values breaks that rule. Generic, so that
    /// nothing initializes a construction but the test asking for it: the registration of the module only registers
    /// the definition.
    /// </summary>
    /// <typeparam name="TSurface">The surface shaded.</typeparam>
    [ValueObject<string>]
    [KnownValue("Light", "light")]
    [KnownValue("Dark", "DARK")]
    private readonly partial struct Shade<TSurface> : IValueObjectValidator<string>
    {
        public static ValidationResult ValidateValue(in string value)
            => string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal)
                ? ValidationResult.Success
                : ValidationResult.InvalidFormat("The shade is not lower case.");
    }

    /// <summary>A value object whose initializer fails for a reason of its own. Generic, as <see cref="Shade{TSurface}"/>.</summary>
    /// <typeparam name="TOwner">The owner of the value.</typeparam>
    [ValueObject<string>]
    private readonly partial struct Broken<TOwner>
    {
        static Broken() => throw new InvalidOperationException("Broken on purpose.");
    }

    /// <summary>
    /// Private, so the runner does not discover it: run whole, it would fail The_type_is_discoverable_at_run_time,
    /// which is exactly right for an unregistered type and not what this class is checking.
    /// </summary>
    private sealed class UnregisteredContract : ValueObjectContract<UnregisteredCode, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["one", "two"];

        protected override IEnumerable<string> RejectedValues => [string.Empty];
    }

    /// <summary>
    /// Private, so the runner does not discover it: the schemas it is used with are out of step on purpose.
    /// </summary>
    /// <typeparam name="TDeclared">The schema the fan speed declares.</typeparam>
    private sealed class FanSpeedContract<TDeclared> : ValueObjectContract<FanSpeed<TDeclared>, int>, IDeclarationChecks
        where TDeclared : IDeclaredSchema
    {
        protected override IEnumerable<int> AcceptedValues => [1, 2];

        protected override IEnumerable<int> RejectedValues => [0, 3];
    }

    /// <summary>The checks of the kit that read the schema the type declares.</summary>
    private interface IDeclarationChecks
    {
        void The_declared_example_is_accepted();

        void Every_declared_known_value_is_accepted();

        void The_known_value_details_line_up_with_the_known_values();
    }

    /// <summary>A schema a value object written by hand declares.</summary>
    private interface IDeclaredSchema
    {
        static abstract ValueObjectSchema Schema { get; }
    }

    /// <summary>Low and high, each detailed in its place, with low as the example.</summary>
    private sealed class LinedUp : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            Example = "1",
            IsClosedValueSet = true,
            KnownValues = [1, 2],
            KnownValueDetails = [new KnownValueInfo(1, "Low", "Quiet."), new KnownValueInfo(2, "High")],
        };
    }

    /// <summary>Low and high, with no detail, as a schema written by hand may leave them.</summary>
    private sealed class Undetailed : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true, KnownValues = [1, 2] };
    }

    /// <summary>Known values and their details both left at the default of their array, which holds nothing.</summary>
    private sealed class Blank : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new() { KnownValues = default, KnownValueDetails = default };
    }

    /// <summary>Low, high and a speed the fan does not run at, both as an example and as a known value.</summary>
    private sealed class Overreaching : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            Example = "3",
            KnownValues = [1, 2, 3],
            KnownValueDetails = [new KnownValueInfo(1, "Low"), new KnownValueInfo(2, "High"), new KnownValueInfo(3, "Turbo")],
        };
    }

    /// <summary>Low and high, high declared as a number of another type, and detailed as such.</summary>
    private sealed class Mistyped : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, 2L],
            KnownValueDetails = [new KnownValueInfo(1, "Low"), new KnownValueInfo(2L, "High")],
        };
    }

    /// <summary>Low and high, of which only low is detailed.</summary>
    private sealed class OneShort : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, 2],
            KnownValueDetails = [new KnownValueInfo(1, "Low")],
        };
    }

    /// <summary>A detail of low, with the known values left at their default, an array that holds nothing.</summary>
    private sealed class Unlisted : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = default,
            KnownValueDetails = [new KnownValueInfo(1, "Low")],
        };
    }

    /// <summary>Low and high, detailed high first.</summary>
    private sealed class Swapped : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, 2],
            KnownValueDetails = [new KnownValueInfo(2, "High"), new KnownValueInfo(1, "Low")],
        };
    }

    /// <summary>Low and high, detailed as numbers of another type, which are other values once boxed.</summary>
    private sealed class Widened : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, 2],
            KnownValueDetails = [new KnownValueInfo(1L, "Low"), new KnownValueInfo(2L, "High")],
        };
    }

    /// <summary>Low and a known value left null, detailed as low and high.</summary>
    private sealed class Unknown : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, null!],
            KnownValueDetails = [new KnownValueInfo(1, "Low"), new KnownValueInfo(2, "High")],
        };
    }

    /// <summary>Low and high, the detail of high left null.</summary>
    private sealed class Missing : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, 2],
            KnownValueDetails = [new KnownValueInfo(1, "Low"), null!],
        };
    }

    /// <summary>
    /// Low and high, the detail of low built without its constructor, as a serializer may build one, so that it has no
    /// name: the constructor refuses one.
    /// </summary>
    private sealed class Nameless : IDeclaredSchema
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            KnownValues = [1, 2],
            KnownValueDetails =
            [
                (KnownValueInfo)RuntimeHelpers.GetUninitializedObject(typeof(KnownValueInfo)),
                new KnownValueInfo(2, "High"),
            ],
        };
    }

    /// <summary>
    /// The speed of a fan, low or high, written by hand, whose schema <typeparamref name="TDeclared"/> declares. Nothing
    /// registers it, and nothing in the kit's check on the details of its known values does either.
    /// </summary>
    /// <typeparam name="TDeclared">The schema it declares.</typeparam>
    private readonly struct FanSpeed<TDeclared> : IValueObject<FanSpeed<TDeclared>, int>
        where TDeclared : IDeclaredSchema
    {
        private readonly int _value;

        private FanSpeed(int value) => _value = value;

        public static ValueObjectSchema Schema => TDeclared.Schema;

        public int Value => _value;

        public bool IsDefault => _value == 0;

        public static int Normalize(int value) => value;

        public static ValidationResult Validate(in int value)
            => value is 1 or 2 ? ValidationResult.Success : ValidationResult.OutOfRange("A fan runs low or high.");

        public static FanSpeed<TDeclared> Create(int value)
        {
            Validate(value).ThrowIfInvalid(typeof(FanSpeed<TDeclared>), value);

            return new FanSpeed<TDeclared>(value);
        }

        public static bool TryCreate(int value, out FanSpeed<TDeclared> result) => TryCreate(value, out result, out _);

        public static bool TryCreate(int value, out FanSpeed<TDeclared> result, out ValidationResult validation)
        {
            validation = Validate(value);
            result = validation.IsValid ? new FanSpeed<TDeclared>(value) : default;

            return validation.IsValid;
        }

        public static FanSpeed<TDeclared> CreateUnchecked(int value) => new(value);

        public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out FanSpeed<TDeclared> result, out ValidationResult validation)
        {
            if (int.TryParse(text, NumberStyles.None, provider, out var raw))
            {
                return TryCreate(raw, out result, out validation);
            }

            result = default;
            validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a fan speed.");

            return false;
        }

        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FanSpeed<TDeclared> result)
            => TryParse(s, provider, out result, out _);

        public static bool TryParse(string? s, IFormatProvider? provider, out FanSpeed<TDeclared> result)
            => TryParse(s.AsSpan(), provider, out result, out _);

        public static FanSpeed<TDeclared> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
            => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a fan speed.");

        public static FanSpeed<TDeclared> Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        public bool Equals(FanSpeed<TDeclared> other) => _value == other._value;

        public override bool Equals(object? obj) => obj is FanSpeed<TDeclared> other && Equals(other);

        public override int GetHashCode() => _value;

        public int CompareTo(FanSpeed<TDeclared> other) => _value.CompareTo(other._value);

        public override string ToString() => ToString(null, null);

        public string ToString(string? format, IFormatProvider? formatProvider)
            => _value.ToString(CultureInfo.InvariantCulture);

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => _value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);

        public static bool operator ==(FanSpeed<TDeclared> left, FanSpeed<TDeclared> right) => left.Equals(right);

        public static bool operator !=(FanSpeed<TDeclared> left, FanSpeed<TDeclared> right) => !left.Equals(right);

        public static bool operator <(FanSpeed<TDeclared> left, FanSpeed<TDeclared> right) => left.CompareTo(right) < 0;

        public static bool operator >(FanSpeed<TDeclared> left, FanSpeed<TDeclared> right) => left.CompareTo(right) > 0;

        public static bool operator <=(FanSpeed<TDeclared> left, FanSpeed<TDeclared> right) => left.CompareTo(right) <= 0;

        public static bool operator >=(FanSpeed<TDeclared> left, FanSpeed<TDeclared> right) => left.CompareTo(right) >= 0;
    }

    /// <summary>The two checks of the kit that read the registry.</summary>
    private interface IRegistryChecks
    {
        void Declared_length_limits_hold_for_every_accepted_value();

        void The_type_is_discoverable_at_run_time();
    }

    /// <summary>
    /// A contract over a rank, from 1 to 10, closed at run time over the copy of the type a test loaded.
    /// </summary>
    /// <typeparam name="TRank">The rank type of that copy.</typeparam>
    private sealed class RankContract<TRank> : ValueObjectContract<TRank, int>, IRegistryChecks
        where TRank : struct, IValueObject<TRank, int>
    {
        protected override IEnumerable<int> AcceptedValues => [1, 10];

        protected override IEnumerable<int> RejectedValues => [0, 11];
    }
}
