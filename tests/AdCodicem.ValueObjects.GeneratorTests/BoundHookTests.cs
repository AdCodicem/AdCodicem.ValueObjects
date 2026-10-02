using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// <c>IValueObjectMinimum&lt;T&gt;</c> and <c>IValueObjectMaximum&lt;T&gt;</c>: bounds a consumer declares as values of the
/// underlying type, which the generated code checks where the text options checked theirs, and publishes as they did.
/// </summary>
public sealed class BoundHookTests
{
    /// <summary>
    /// The bound is read through the bridge in the check, the message and the schema alike. A floating-point bound asks
    /// whether the value is inside it, so that NaN, which compares false with everything, is refused.
    /// </summary>
    [Fact]
    public void A_bound_declared_through_a_hook_is_checked_quoted_and_published()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<DateOnly>]
            public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
            {
                public static DateOnly Minimum { get; } = new(1900, 1, 1);

                public static DateOnly Maximum => new(2100, 12, 31);
            }

            [ValueObject<double>]
            public readonly partial struct Latitude : IValueObjectMinimum<double>
            {
                public static double Minimum => -90;
            }

            [ValueObject<float>]
            public readonly partial struct Ratio : IValueObjectMaximum<float>
            {
                public static float Maximum => 1;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        const string Minimum = "global::AdCodicem.ValueObjects.ValueObjectBound.Minimum<global::Test.BirthDate, global::System.DateOnly>()";
        Generated(run, "BirthDate").Should()
            .Contain($"if (value < {Minimum})")
            .And.Contain($"\"The value must be greater than or equal to \" + global::AdCodicem.ValueObjects.ValueObjectBound.Text({Minimum}) + \".\"")
            .And.Contain($"Minimum = global::AdCodicem.ValueObjects.ValueObjectBound.Text({Minimum}),")
            .And.Contain("Maximum = global::AdCodicem.ValueObjects.ValueObjectBound.Text(global::AdCodicem.ValueObjects.ValueObjectBound.Maximum<global::Test.BirthDate, global::System.DateOnly>()),");
        Generated(run, "Latitude").Should()
            .Contain("if (!(value >= global::AdCodicem.ValueObjects.ValueObjectBound.Minimum<global::Test.Latitude, global::System.Double>()))")
            .And.NotContain("Maximum =");
        Generated(run, "Ratio").Should()
            .Contain("if (!(value <= global::AdCodicem.ValueObjects.ValueObjectBound.Maximum<global::Test.Ratio, global::System.Single>()))")
            .And.NotContain("Minimum =");
    }

    /// <summary>
    /// A static abstract member is reached through a type parameter, which finds an explicit implementation too.
    /// </summary>
    [Fact]
    public void A_bound_implemented_explicitly_compiles()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Floor : IValueObjectMinimum<int>
            {
                static int IValueObjectMinimum<int>.Minimum => -5;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The hook replaces the deprecated option. Declaring both is an error, after which the hook wins, so that every
    /// use of the type does not fail as well.
    /// </summary>
    [Fact]
    public void A_bound_declared_twice_is_reported_and_the_hook_wins()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<int>(Minimum = "0", Maximum = "9")]
            public readonly partial struct Digit : IValueObjectMinimum<int>
            {
                public static int Minimum => 1;
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0029");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Digit' sets the Minimum option and implements IValueObjectMinimum<int>. The hook replaces the option: "
            + "remove Minimum = \"...\" and keep the Minimum property.");
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should()
            .Contain("ValueObjectBound.Minimum<global::Test.Digit, global::System.Int32>()")
            .And.NotContain("Minimum = \"0\"")
            .And.Contain("Maximum = \"9\"", "the other bound is still the option's");
    }

    [Fact]
    public void A_maximum_declared_twice_is_reported_and_the_hook_wins()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<int>(Maximum = "9")]
            public readonly partial struct Digit : IValueObjectMaximum<int>
            {
                public static int Maximum => 8;
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0029");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Digit' sets the Maximum option and implements IValueObjectMaximum<int>. The hook replaces the option: "
            + "remove Maximum = \"...\" and keep the Maximum property.");
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should()
            .Contain("ValueObjectBound.Maximum<global::Test.Digit, global::System.Int32>()")
            .And.NotContain("Maximum = \"9\"")
            .And.NotContain("Minimum =");
    }

    /// <summary>
    /// The compiler accepts the interface over any type, on any value object. A bound over a type that takes none, or
    /// over another type than the underlying one, would be declared and never checked: it is reported instead.
    /// </summary>
    /// <param name="declaration">The value object.</param>
    /// <param name="hook">The hook as the message names it.</param>
    /// <param name="underlying">The underlying type as the message names it.</param>
    /// <param name="reason">The end of the message.</param>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>]
        public readonly partial struct Code : IValueObjectMinimum<string>
        {
            public static string Minimum => "A";
        }
        """,
        "IValueObjectMinimum<string>",
        "string",
        "A string takes no bound: constrain its length with MinLength or MaxLength, and its form with "
        + "IValueObjectPatternValidator or a validator hook.")]
    [InlineData(
        """
        [ValueObject<bool>]
        public readonly partial struct Flag : IValueObjectMaximum<bool>
        {
            public static bool Maximum => true;
        }
        """,
        "IValueObjectMaximum<bool>",
        "bool",
        "A value object over that type takes no bound: remove the hook, or check the value in a validator hook.")]
    [InlineData(
        """
        [ValueObject<Guid>]
        public readonly partial struct Token : IValueObjectMinimum<Guid>
        {
            public static Guid Minimum => Guid.Empty;
        }
        """,
        "IValueObjectMinimum<Guid>",
        "System.Guid",
        "A value object over that type takes no bound: remove the hook, or check the value in a validator hook.")]
    [InlineData(
        """
        [ValueObject<long>]
        public readonly partial struct Total : IValueObjectMinimum<int>
        {
            public static int Minimum => 0;
        }
        """,
        "IValueObjectMinimum<int>",
        "long",
        "A bound is a value of the underlying type: implement IValueObjectMinimum<long> instead.")]
    [InlineData(
        """
        [EntityId("acc")]
        public readonly partial struct AccountId : IValueObjectMaximum<string>
        {
            public static string Maximum => "acc_";
        }
        """,
        "IValueObjectMaximum<string>",
        "string",
        "An identifier's format is fixed: check anything more in a validator hook.")]
    public void A_bound_hook_that_cannot_bound_the_type_is_reported(string declaration, string hook, string underlying, string reason)
    {
        var run = GeneratorHarness.Run(declaration);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0030");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().EndWith(
            $"implements {hook}, which cannot bound a value object over '{underlying}'. {reason}");
        run.CompilationDiagnostics.Should().BeEmpty("the type still generates, without the bound");
        run.SingleValueObject.Should().NotContain("ValueObjectBound");
    }

    /// <summary>
    /// A hook over the underlying type does not excuse a second one over another type, which nothing would check.
    /// </summary>
    [Fact]
    public void A_bound_hook_over_another_type_is_reported_beside_one_over_the_underlying_type()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<long>]
            public readonly partial struct Total : IValueObjectMinimum<long>, IValueObjectMinimum<int>
            {
                static long IValueObjectMinimum<long>.Minimum => 0;

                static int IValueObjectMinimum<int>.Minimum => 0;
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0030");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Contain("implements IValueObjectMinimum<int>,");
        run.SingleValueObject.Should().Contain("ValueObjectBound.Minimum<global::Test.Total, global::System.Int64>()");
    }

    [Fact]
    public async Task The_analyzer_reports_a_bound_written_without_its_interface()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>("""
            [ValueObject<DateOnly>]
            public readonly partial struct BirthDate : IValueObjectMaximum<DateOnly>
            {
                public static DateOnly Minimum => new(1900, 1, 1);

                public static DateOnly Maximum => new(2100, 12, 31);
            }

            [ValueObject<int>]
            public readonly partial struct Floor
            {
                public static int Maximum { get; } = 200;
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "'Minimum' looks like a value object rule but 'BirthDate' does not implement 'IValueObjectMinimum<DateOnly>'. "
            + "Declare the interface, or the rule will never run.",
            "'Maximum' looks like a value object rule but 'Floor' does not implement 'IValueObjectMaximum<int>'. "
            + "Declare the interface, or the rule will never run.");
    }

    /// <summary>
    /// A bound a validator may check or a normalizer clamp to itself, as they had to before the hooks existed, one the
    /// deprecated option still sets, a field, which could not implement the hook, one of another type, one that is not
    /// public, not static or not a property, and one on a type that takes no bound are no hook written without its
    /// interface.
    /// </summary>
    [Fact]
    public async Task The_analyzer_leaves_alone_what_is_no_bound_hook()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>("""
            [ValueObject<int>]
            public readonly partial struct Checked : IValueObjectValidator<int>
            {
                public static int Minimum => 1;

                public static ValidationResult ValidateValue(in int value)
                    => value >= Minimum ? ValidationResult.Success : ValidationResult.OutOfRange("Too small.");
            }

            [ValueObject<double>]
            public readonly partial struct Opacity : IValueObjectNormalizer<double>
            {
                public static double Minimum => 0;

                public static double Maximum => 1;

                public static double NormalizeValue(double value) => Math.Clamp(value, Minimum, Maximum);
            }

            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<int>(Maximum = "100")]
            public readonly partial struct Percentage
            {
                public static int Maximum => 100;
            }
            #pragma warning restore VO0028

            [ValueObject<int>]
            public readonly partial struct PageSize
            {
                public const int Minimum = 1;

                public static readonly int Maximum = 100;
            }

            [ValueObject<int>]
            public readonly partial struct Counted
            {
                public static long Minimum => 1;

                private static int Maximum => 9;
            }

            [ValueObject<int>]
            public readonly partial struct Rank
            {
                public int Minimum => 1;

                public static int Maximum() => 9;
            }

            [ValueObject<string>]
            public readonly partial struct Code
            {
                public static string Minimum => "A";
            }

            [ValueObject<Guid>]
            public readonly partial struct Token
            {
                public static Guid Minimum => Guid.Empty;
            }

            [ValueObject<bool>]
            public readonly partial struct Flag
            {
                public static bool Maximum => true;
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    private static string Generated(GeneratorRun run, string name)
        => run.Files.Single(file => file.HintName == AdCodicem.ValueObjects.Generators.Internal.HintNames.For($"Test.{name}")).Text;
}
