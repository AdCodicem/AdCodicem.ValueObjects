using System.Text;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What the generator emits for a well-formed value object.
/// </summary>
/// <remarks>
/// The unit tests exercise generated code by using it, which proves it behaves but not that it exists: a member
/// silently not emitted looks identical to one that was never asked for. These tests read the output.
/// </remarks>
public sealed class EmissionTests
{
    [Fact]
    public void A_string_value_object_compiles_with_no_diagnostics()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Should().HaveCount(2, "the value object and the assembly registration");
    }

    [Fact]
    public void The_struct_holds_exactly_its_underlying_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        // The whole memory argument for a struct value object rests on this single field.
        run.SingleValueObject.Should().Contain("private readonly global::System.String? _value;");
    }

    /// <summary>
    /// A reflection mapper constructs a type through any constructor taking the source value alone, private ones
    /// included. The one constructor the generator writes takes, after the value, a required tag only the generated
    /// code supplies, so a mapper finds no constructor it can call; an optional one it would fill from its default.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="name">The name of the value object.</param>
    /// <param name="value">The underlying type, fully qualified.</param>
    [Theory]
    [InlineData("[ValueObject<string>(MaxLength = 12)] public readonly partial struct Code;", "Code", "global::System.String")]
    [InlineData("[ValueObject<int>(Arithmetic = true)] public readonly partial struct Count;", "Count", "global::System.Int32")]
    [InlineData("[ValueObject<global::System.Guid>] public readonly partial struct OrderId;", "OrderId", "global::System.Guid")]
    [InlineData("[EntityId(\"acc\")] public readonly partial struct AccountId;", "AccountId", "global::System.String")]
    [InlineData("[ValueObject<decimal>] public readonly partial struct Price<TCurrency>;", "Price", "global::System.Decimal")]
    public void The_only_constructor_takes_a_required_tag_after_the_value(string declaration, string name, string value)
    {
        var run = GeneratorHarness.Run(declaration);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var constructor = CSharpSyntaxTree.ParseText(run.SingleValueObject, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Where(candidate => candidate.Identifier.Text == name)
            .Should().ContainSingle().Subject;

        constructor.Modifiers.Select(modifier => modifier.Text).Should().Equal("private");
        constructor.ParameterList.Parameters.Select(parameter => parameter.Type!.ToString())
            .Should().Equal(value, "global::AdCodicem.ValueObjects.UncheckedTag");
        constructor.ParameterList.Parameters.Should().OnlyContain(parameter => parameter.Default == null);
    }

    [Fact]
    public void The_contract_members_are_all_emitted()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var generated = run.SingleValueObject;

        foreach (var member in new[]
        {
            "public static global::Test.Code Create(",
            "public static bool TryCreate(",
            "public static global::Test.Code CreateUnchecked(",
            "public static global::Test.Code Parse(",
            "public static bool TryParse(",
            "public bool Equals(global::Test.Code",
            "public override int GetHashCode()",
            "public int CompareTo(global::Test.Code",
            "public bool TryFormat(",
            "public override string ToString()",
        })
        {
            generated.Should().Contain(member);
        }
    }

    /// <summary>
    /// The schema the generator emits is the static member the contract declares, so generic code reads it through its
    /// type parameter, over a value object and over a construction of a generic one, with no registry.
    /// </summary>
    [Fact]
    public void The_emitted_schema_implements_the_static_member_of_the_contract()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(MaxLength = 12)]
            public readonly partial struct Code;

            [ValueObject<int>]
            public readonly partial struct Box<TOwner>;

            public static class Reader
            {
                public static int? MaxLength<TSelf, TValue>()
                    where TSelf : struct, IValueObject<TSelf, TValue>
                    => TSelf.Schema.MaxLength;

                public static int? OfCode() => MaxLength<Code, string>();

                public static int? OfBox() => MaxLength<Box<Code>, int>();
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Where(file => !file.HintName.Contains("Registration", StringComparison.Ordinal))
            .Should().HaveCount(2)
            .And.OnlyContain(file => file.Text.Contains(
                "public static global::AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema { get; } = new()",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// A tool reading the public instance properties of a type publishes each as data, so <c>IsDefault</c>, a guard
    /// for code, is implemented explicitly: over a string it tells an instance holding no string, over a value type an
    /// instance equal to the type's zero.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="implementation">The implementation expected.</param>
    [Theory]
    [InlineData(
        "[ValueObject<string>] public readonly partial struct Code;",
        "bool global::AdCodicem.ValueObjects.IValueObject<global::Test.Code, global::System.String>.IsDefault => _value is null;")]
    [InlineData(
        "[ValueObject<decimal>(Arithmetic = true)] public readonly partial struct Amount;",
        "bool global::AdCodicem.ValueObjects.IValueObject<global::Test.Amount, global::System.Decimal>.IsDefault => _value.Equals(default(global::System.Decimal));")]
    [InlineData(
        "[EntityId(\"acc\")] public readonly partial struct AccountId;",
        "bool global::AdCodicem.ValueObjects.IValueObject<global::Test.AccountId, global::System.String>.IsDefault => _value is null;")]
    [InlineData(
        "[ValueObject<global::System.Guid>] public readonly partial struct Key<T>;",
        "bool global::AdCodicem.ValueObjects.IValueObject<global::Test.Key<T>, global::System.Guid>.IsDefault => _value.Equals(default(global::System.Guid));")]
    public void IsDefault_is_an_explicit_implementation_of_the_contract(string declaration, string implementation)
    {
        var run = GeneratorHarness.Run(declaration);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(implementation).And.NotContain("public bool IsDefault");
    }

    /// <summary>
    /// Both writers of the System.Text.Json converter first refuse a value the type rejects, which only an instance
    /// equal to the default can hold: the check compares the field as <c>IsDefault</c> does, then validates the value as
    /// it stands, and throws a <c>ValueObjectJsonException</c> naming the type and the rule, and carrying its code.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="name">The name of the value object, as messages quote it.</param>
    /// <param name="condition">The comparison with the default.</param>
    [Theory]
    [InlineData("[ValueObject<string>(MaxLength = 12)] public readonly partial struct Code;", "Code", "value._value is null")]
    [InlineData("[ValueObject<int>(Arithmetic = true)] public readonly partial struct Count;", "Count", "value._value.Equals(default(global::System.Int32))")]
    [InlineData("[ValueObject<global::System.Guid>] public readonly partial struct OrderId;", "OrderId", "value._value.Equals(default(global::System.Guid))")]
    [InlineData("[EntityId(\"acc\")] public readonly partial struct AccountId;", "AccountId", "value._value is null")]
    [InlineData("[ValueObject<decimal>] public readonly partial struct Price<TCurrency>;", "Price", "value._value.Equals(default(global::System.Decimal))")]
    public void Both_JSON_writers_first_refuse_a_value_the_type_rejects(string declaration, string name, string condition)
    {
        var run = GeneratorHarness.Run(declaration);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var methods = CSharpSyntaxTree.ParseText(run.SingleValueObject, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Should().ContainSingle(candidate => candidate.Identifier.Text == "ValueJsonConverter").Subject
            .Members.OfType<MethodDeclarationSyntax>()
            .ToList();

        foreach (var writer in new[] { "Write", "WriteAsPropertyName" })
        {
            methods.Should().ContainSingle(method => method.Identifier.Text == writer).Subject
                .Body!.Statements.First().ToString().Should().Be("ThrowIfRefused(in value);", "{0} checks before it writes", writer);
        }

        var check = methods.Should().ContainSingle(method => method.Identifier.Text == "ThrowIfRefused").Subject;
        check.Modifiers.Select(modifier => modifier.Text).Should().Equal("private", "static");
        check.Body!.Statements.Should().ContainSingle().Which.Should().BeOfType<IfStatementSyntax>()
            .Which.Condition.ToString().Should().Be(condition);
        check.ToString().Should().Contain(".Validate(in current);")
            .And.Contain($"throw new global::AdCodicem.ValueObjects.ValueObjectJsonException($\"The value to write is not a valid {name}: {{validation.ErrorMessage}}\", typeof(");
    }

    /// <summary>
    /// Every refusal of the System.Text.Json converter, at each of its throw sites, is a <c>ValueObjectJsonException</c>
    /// naming the value object and carrying a code, and no plain <c>JsonException</c> is left. A token the reader's
    /// <c>TryGet</c> method cannot read is refused without a message, which System.Text.Json writes as it writes one for
    /// the reader's own refusal; a null, which the default arm alone receives, is <c>value_object.required</c>.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="self">The value object, fully qualified.</param>
    /// <param name="throws">The throw sites of the converter.</param>
    /// <param name="tryRead">The reader method the converter reads a token with, if it cannot fail on its own.</param>
    [Theory]
    [InlineData("[ValueObject<string>(MaxLength = 12)] public readonly partial struct Code;", "global::Test.Code", 4, null)]
    [InlineData("[ValueObject<string>] public readonly partial struct Name : IValueObjectSpanNormalizer { public static string NormalizeValue(global::System.ReadOnlySpan<char> value) => value.ToString(); }", "global::Test.Name", 5, null)]
    [InlineData("[ValueObject<bool>] public readonly partial struct Flag;", "global::Test.Flag", 4, null)]
    [InlineData("[ValueObject<int>(Arithmetic = true)] public readonly partial struct Count;", "global::Test.Count", 6, "TryGetInt32")]
    [InlineData("[ValueObject<byte>] public readonly partial struct Level;", "global::Test.Level", 6, "TryGetByte")]
    [InlineData("[ValueObject<double>] public readonly partial struct Ratio;", "global::Test.Ratio", 6, "TryGetDouble")]
    [InlineData("[ValueObject<decimal>] public readonly partial struct Price<TCurrency>;", "global::Test.Price<TCurrency>", 6, "TryGetDecimal")]
    [InlineData("[ValueObject<global::System.Guid>] public readonly partial struct OrderId;", "global::Test.OrderId", 5, "TryGetGuid")]
    [InlineData("[ValueObject<global::System.DateTimeOffset>] public readonly partial struct At;", "global::Test.At", 5, "TryGetDateTimeOffset")]
    [InlineData("[ValueObject<char>] public readonly partial struct Grade;", "global::Test.Grade", 5, null)]
    [InlineData("[ValueObject<global::System.DateOnly>] public readonly partial struct Day;", "global::Test.Day", 5, null)]
    [InlineData("[ValueObject<global::System.Int128>] public readonly partial struct Balance;", "global::Test.Balance", 5, null)]
    [InlineData("[EntityId(\"acc\")] public readonly partial struct AccountId;", "global::Test.AccountId", 4, null)]
    public void Every_refusal_of_the_JSON_converter_carries_the_type_and_a_code(string declaration, string self, int throws, string? tryRead)
    {
        var run = GeneratorHarness.Run(declaration);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var converter = CSharpSyntaxTree.ParseText(run.SingleValueObject, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Should().ContainSingle(candidate => candidate.Identifier.Text == "ValueJsonConverter").Subject;
        var refusals = converter.DescendantNodes().OfType<ThrowStatementSyntax>().Select(statement => statement.Expression!)
            .Concat(converter.DescendantNodes().OfType<ThrowExpressionSyntax>().Select(expression => expression.Expression))
            .ToList();

        converter.ToString().Should().NotContain("global::System.Text.Json.JsonException");
        refusals.Should().HaveCount(throws);
        foreach (var refusal in refusals)
        {
            var creation = refusal.Should().BeOfType<ObjectCreationExpressionSyntax>().Subject;
            creation.Type.ToString().Should().Be("global::AdCodicem.ValueObjects.ValueObjectJsonException");
            var arguments = creation.ArgumentList!.Arguments.Select(argument => argument.ToString()).ToList();
            arguments.Should().HaveCount(3);
            arguments[1].Should().Be($"typeof({self})");
            arguments[2].Should().Match(
                code => code == "validation.ErrorCode" || code.Contains("global::AdCodicem.ValueObjects.ValueObjectErrorCodes."),
                "a refusal carries the code of the rule, which the write check has from a failed validation, or a code of the framework");
        }

        refusals.Select(refusal => ((ObjectCreationExpressionSyntax)refusal).ArgumentList!.Arguments[2].ToString())
            .Should().Contain("reader.TokenType == global::System.Text.Json.JsonTokenType.Null ? global::AdCodicem.ValueObjects.ValueObjectErrorCodes.Required : global::AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable", "a null is no value supplied");

        if (tryRead is null)
        {
            converter.ToString().Should().NotContain("reader.TryGet");
            return;
        }

        var read = converter.DescendantNodes().OfType<IfStatementSyntax>()
            .Should().ContainSingle(statement => statement.Condition.ToString() == $"!reader.{tryRead}(out raw)").Subject;
        read.Statement.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single().ArgumentList!.ToString()
            .Should().Be($"(null, typeof({self}), global::AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable)");
        converter.ToString().Should().NotContain($"reader.{tryRead.Replace("TryGet", "Get", StringComparison.Ordinal)}()");
    }

    [Fact]
    public void A_value_object_serializes_as_its_underlying_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var generated = run.SingleValueObject;

        // Writing the bare value, rather than an object wrapper, is the point of the whole library.
        generated.Should().Contain("writer.WriteStringValue(value.Value);");
        generated.Should().Contain("public sealed class ValueJsonConverter");
    }

    [Fact]
    public void Arithmetic_is_emitted_only_when_asked_for()
    {
        var without = GeneratorHarness.Run("""
            [ValueObject<decimal>]
            public readonly partial struct Money;
            """);

        var with = GeneratorHarness.Run("""
            [ValueObject<decimal>(Arithmetic = true)]
            public readonly partial struct Money;
            """);

        without.SingleValueObject.Should().NotContain("operator +");
        with.SingleValueObject.Should().Contain("operator +");
        with.SingleValueObject.Should().Contain("public static global::Test.Money Zero");
    }

    [Fact]
    public void Integral_arithmetic_is_checked_so_an_overflow_throws_rather_than_wraps()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Count;
            """);

        run.SingleValueObject.Should().Contain("checked(");
    }

    [Fact]
    public void A_closed_value_set_emits_named_constants_and_a_frozen_lookup()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("France", "FR")]
            [KnownValue("Belgium", "BE")]
            public readonly partial struct Country;
            """);

        var generated = run.SingleValueObject;

        run.CompilationDiagnostics.Should().BeEmpty();
        generated.Should().Contain("public static global::Test.Country France { get; } = Create(\"FR\");");
        generated.Should().Contain("public static global::Test.Country Belgium { get; } = Create(\"BE\");");
        generated.Should().Contain("FrozenSet");
    }

    [Fact]
    public void A_nested_value_object_reopens_every_containing_type()
    {
        var run = GeneratorHarness.Run("""
            public static partial class Outer
            {
                public static partial class Inner
                {
                    [ValueObject<string>]
                    public readonly partial struct Code;
                }
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("partial class Outer").And.Contain("partial class Inner");
    }

    /// <summary>
    /// The generated code reopens each containing type with the keyword it was declared with: a record struct
    /// reopened as a struct, or a record as a class, is a different declaration the compiler refuses.
    /// </summary>
    [Theory]
    [InlineData("public partial record struct Outer", "partial record struct Outer")]
    [InlineData("public partial record Outer", "partial record Outer")]
    [InlineData("public readonly partial struct Outer", "partial struct Outer")]
    [InlineData("public static partial class Outer", "partial class Outer")]
    public void A_nested_value_object_reopens_its_container_as_declared(string container, string reopening)
    {
        var run = GeneratorHarness.Run($$"""
            {{container}}
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Split('\n').Select(line => line.Trim()).Should().Contain(reopening);
    }

    /// <summary>
    /// Roslyn compares hint names ignoring case, and a second file under a name already added throws, after which the
    /// generator contributes nothing to the compilation, for any value object. Types whose names differ by case only,
    /// or by a character no file name holds, each get a file of their own.
    /// </summary>
    [Fact]
    public void Types_whose_names_read_the_same_in_a_file_name_each_get_a_file_of_their_own()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Code;

            [ValueObject<int>]
            public readonly partial struct CODE;

            [ValueObject<int>]
            public readonly partial struct @event;

            [ValueObject<int>]
            public readonly partial struct _event;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        run.Files.Should().HaveCount(5);
        run.Files.Select(file => file.HintName.ToUpperInvariant()).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// A combining mark is part of an identifier but no letter or digit, so it has no place in the name of the
    /// generated file and is replaced there, while the code keeps the name as declared. The hash that follows is the
    /// exact name's.
    /// </summary>
    [Fact]
    public void A_name_holding_a_character_outside_the_file_name_alphabet_gets_a_hint_name_without_it()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Cafe\u0301Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For("Test.Cafe\u0301Code"), "ValueObjectRegistration.g.cs");
        run.Files.Should().Contain(file => file.HintName.StartsWith("Test.Cafe_Code.", StringComparison.Ordinal));
        run.SingleValueObject.Should().Contain("partial struct Cafe\u0301Code : ");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A keyword is a legal name for a type or a namespace once escaped, so the generated code has to write it
    /// escaped wherever it names one: the namespace, the containing types, the value object, its constructor, and
    /// the documentation references, which only a project producing its documentation file resolves.
    /// </summary>
    [Fact]
    public void A_value_object_named_after_a_keyword_is_written_with_its_escape()
    {
        var run = GeneratorHarness.Run(
            """
            using AdCodicem.ValueObjects.Annotations;

            namespace @class.@namespace;

            public static partial class @static
            {
                [ValueObject<string>]
                public readonly partial struct @event;
            }
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().OnlyContain(
            diagnostic => diagnostic.Id == "CS8981" && diagnostic.Location.SourceTree!.FilePath.Length == 0,
            "a lower-case type name is the author's to answer for, on their declaration and nowhere else");

        var generated = run.SingleValueObject;
        generated.Should().Contain("namespace @class.@namespace");
        generated.Should().Contain("partial class @static");
        generated.Should().Contain("partial struct @event : ");
        generated.Should().Contain("private @event(");
        generated.Should().Contain("cref=\"@event\"");
    }

    /// <summary>
    /// The generated code declares again the names of the value object and of the types around it. A lower-case one
    /// draws CS8981 where the author declared it, which the author can silence there, and nowhere else.
    /// </summary>
    [Fact]
    public void A_lower_case_name_is_reported_on_the_declaration_alone()
    {
        var run = GeneratorHarness.Run("""
            public partial class ledger
            {
                [ValueObject<int>]
                public readonly partial struct tally;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().NotBeEmpty().And.OnlyContain(
            diagnostic => diagnostic.Id == "CS8981" && diagnostic.Location.SourceTree!.FilePath.Length == 0,
            "the generated declarations repeat the names, which the author answers for on their own");
    }

    [Fact]
    public void Conversions_are_opt_in()
    {
        var without = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var with = GeneratorHarness.Run("""
            [ValueObject<string>(ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
            public readonly partial struct Code;
            """);

        without.SingleValueObject.Should().NotContain("operator global::System.String");
        with.SingleValueObject.Should().Contain("implicit operator global::System.String");
        with.SingleValueObject.Should().Contain("explicit operator global::Test.Code");
    }

    /// <summary>
    /// The complement of the range diagnostics: the extremes of every integer type are bounds like any other.
    /// </summary>
    [Theory]
    [InlineData("sbyte", "-128", "127", "(sbyte)(127)")]
    [InlineData("byte", "0", "255", "(byte)(255)")]
    [InlineData("short", "-32768", "32767", "(short)(-32768)")]
    [InlineData("ushort", "0", "65535", "(ushort)(65535)")]
    [InlineData("int", "-2147483648", "2147483647", "(int)(-2147483648)")]
    [InlineData("uint", "0", "4294967295", "4294967295U")]
    [InlineData("long", "-9223372036854775808", "9223372036854775807", "-9223372036854775808L")]
    [InlineData("ulong", "0", "18446744073709551615", "18446744073709551615UL")]
    [InlineData(
        "Int128",
        "-170141183460469231731687303715884105728",
        "170141183460469231731687303715884105727",
        "new global::System.Int128(9223372036854775807UL, 18446744073709551615UL)")]
    [InlineData(
        "UInt128",
        "0",
        "340282366920938463463374607431768211455",
        "new global::System.UInt128(18446744073709551615UL, 18446744073709551615UL)")]
    [InlineData("long", "-0", "1", "(value < 0L)")]
    [InlineData("int", "007", "8", "(int)(7)")]
    public void An_integer_bound_at_the_extremes_of_its_type_compiles(
        string underlying,
        string minimum,
        string maximum,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<{{underlying}}>(Minimum = "{{minimum}}", Maximum = "{{maximum}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    [Theory]
    [InlineData("double", "0.5", "1e3", "1000d")]
    [InlineData("double", "-1.7976931348623157E+308", "1.7976931348623157E+308", "1.7976931348623157E+308d")]
    [InlineData("float", "0.5", "2.5", "2.5f")]
    [InlineData("float", "-3.4028235E+38", "3.4028235E+38", "3.4028235E+38f")]
    public void A_finite_floating_point_bound_compiles(string underlying, string minimum, string maximum, string literal)
    {
        var run = GeneratorHarness.Run($$"""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<{{underlying}}>(Minimum = "{{minimum}}", Maximum = "{{maximum}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    /// <summary>
    /// The ticks a date and time bound compiles to are those written, whatever the time zone of the machine
    /// running the compiler.
    /// </summary>
    [Theory]
    [InlineData(
        "DateTime",
        "2020-01-01T08:30:00",
        "new global::System.DateTime(637134642000000000L, global::System.DateTimeKind.Unspecified)")]
    [InlineData(
        "DateTimeOffset",
        "2020-01-01T00:00:00+02:00",
        "new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(72000000000L))")]
    [InlineData(
        "DateTimeOffset",
        "2020-01-01T00:00:00Z",
        "new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(0L))")]
    public void A_date_and_time_bound_compiles_to_the_instant_written(string underlying, string bound, string literal)
    {
        var run = GeneratorHarness.Run($$"""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<{{underlying}}>(Minimum = "{{bound}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    /// <summary>
    /// C# ends a line at U+0085, U+2028 and U+2029 as well as at a line feed, and a regular string or character
    /// literal cannot hold any of them raw.
    /// </summary>
    [Fact]
    public void A_unicode_line_terminator_in_author_text_is_escaped_in_every_literal()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0021 // The deprecated option is what this test declares.
            [ValueObject<string>(Description = "One\u2028two", Example = "a\u0085b", Pattern = "^[^\u2029]+$")]
            [KnownValue("Separated", "a\u2028b\u2029c\u0085d")]
            public readonly partial struct Token;

            [ValueObject<char>]
            [KnownValue("LineSeparator", '\u2028')]
            [KnownValue("NextLine", "\u0085")]
            public readonly partial struct Separator;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var token = run.Files.Single(file => file.HintName.Contains("Token", StringComparison.Ordinal)).Text;
        token.Should().Contain("Create(\"a\\u2028b\\u2029c\\u0085d\")");
        token.Should().Contain("Description = \"One\\u2028two\"");
        token.Should().Contain("Example = \"a\\u0085b\"");
        token.Should().Contain("\"^[^\\u2029]+$\"");

        var separator = run.Files.Single(file => file.HintName.Contains("Separator", StringComparison.Ordinal)).Text;
        separator.Should().Contain("Create('\\u2028')");
        separator.Should().Contain("Create('\\u0085')");
    }

    /// <summary>
    /// A generated file is written as UTF-8, which cannot hold half of a surrogate pair: written raw, a lone
    /// surrogate becomes U+FFFD in the file on disk and in the source embedded for the debugger, while the compiler
    /// read another string. It is escaped in every literal, as a line terminator is, and a whole pair, which stands
    /// for one character, is kept as written.
    /// </summary>
    [Fact]
    public void A_lone_surrogate_in_author_text_is_escaped_in_every_literal()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0021 // The deprecated option is what this test declares.
            [ValueObject<string>(Description = "One\uD800", Example = "\uDC00b", Pattern = "^[^\uDBFF]+$")]
            [KnownValue("Broken", "a\uD800b\uDC00")]
            [KnownValue("Reversed", "\uDE00\uD83D")]
            [KnownValue("Paired", "\uD83D\uDE00")]
            public readonly partial struct Token;

            [ValueObject<char>]
            [KnownValue("High", '\uD800')]
            [KnownValue("Low", "\uDFFF")]
            public readonly partial struct Half;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var token = run.Files.Single(file => file.HintName.Contains("Token", StringComparison.Ordinal)).Text;
        token.Should().Contain("Create(\"a\\uD800b\\uDC00\")");
        token.Should().Contain("Create(\"\\uDE00\\uD83D\")");
        token.Should().Contain("Create(\"\uD83D\uDE00\")");
        token.Should().Contain("Description = \"One\\uD800\"");
        token.Should().Contain("Example = \"\\uDC00b\"");
        token.Should().Contain("\"^[^\\uDBFF]+$\"");

        var half = run.Files.Single(file => file.HintName.Contains("Half", StringComparison.Ordinal)).Text;
        half.Should().Contain("Create('\\uD800')");
        half.Should().Contain("Create('\\uDFFF')");

        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        run.Files.Should().AllSatisfy(file => strict.Invoking(encoding => encoding.GetBytes(file.Text)).Should().NotThrow());
    }

    /// <summary>
    /// The description of a known value becomes a one-line documentation comment: a line break would end the
    /// comment and leave the rest of the text as code.
    /// </summary>
    [Fact]
    public void A_known_value_description_spanning_several_lines_is_folded_into_one_summary_line()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR", Description = "The French Republic,\nmainland\r\nand\u2028overseas\u0001<&>")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(
            "/// <summary>The French Republic, mainland and overseas &lt;&amp;&gt;</summary>");
    }

    [Fact]
    public void A_known_value_description_keeps_its_tabs_and_folds_every_other_line_break()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR", Description = "French\rRépublique\tFR\u0085mainland\u2029overseas\u20AC\r")]
            public readonly partial struct Country;
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("/// <summary>French République\tFR mainland overseas€ </summary>");
    }

    /// <summary>
    /// XML admits neither U+FFFE nor U+FFFF, nor half of a surrogate pair, so a project producing its documentation
    /// file reports a summary holding one as malformed. A whole pair stands for one character and stays.
    /// </summary>
    [Fact]
    public void A_known_value_description_folds_the_characters_xml_cannot_hold_and_keeps_a_surrogate_pair()
    {
        var run = GeneratorHarness.Run(
            """
            [ValueObject<string>]
            [KnownValue("France", "FR", Description = "a\uFFFEb\uFFFFc\uD800d\uDC00e\uD83D\uDE00f\uDBFF")]
            public readonly partial struct Country;
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("/// <summary>a b c d e\uD83D\uDE00f </summary>");
    }

    /// <summary>
    /// The message of a violated bound quotes the bound as written, inside a string literal of the generated code,
    /// so the text has to be escaped for C#, not for XML.
    /// </summary>
    [Fact]
    public void A_bound_is_quoted_in_its_message_as_written()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<char>(Minimum = "\"", Maximum = "\\")]
            public readonly partial struct Quoted;

            [ValueObject<char>(Minimum = "<")]
            public readonly partial struct Angled;

            [ValueObject<char>(Minimum = "\n")]
            public readonly partial struct Spaced;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        string Generated(string name) => run.Files.Single(file => file.HintName == HintNames.For($"Test.{name}")).Text;

        Generated("Quoted").Should()
            .Contain("""OutOfRange("The value must be greater than or equal to \".")""")
            .And.Contain("""OutOfRange("The value must be less than or equal to \\.")""");
        Generated("Angled").Should().Contain("""OutOfRange("The value must be greater than or equal to <.")""");
        Generated("Spaced").Should().Contain("""OutOfRange("The value must be greater than or equal to \n.")""");
    }

    [Fact]
    public void A_value_object_declared_outside_any_namespace_compiles()
    {
        var run = GeneratorHarness.Run("""
            using AdCodicem.ValueObjects.Annotations;
            using AdCodicem.ValueObjects.Identifiers;

            // In the global namespace: the harness wraps a snippet in a namespace only when its text never mentions one.
            [ValueObject<string>]
            public readonly partial struct GlobalCode;

            [EntityId("glb")]
            public readonly partial struct GlobalId;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName)
            .Should().BeEquivalentTo(HintNames.For("GlobalCode"), HintNames.For("GlobalId"), "ValueObjectRegistration.g.cs");
        run.Files.Where(file => file.HintName != "ValueObjectRegistration.g.cs")
            .Should().AllSatisfy(file => file.Text.Should().NotContain("namespace "));
    }

    /// <summary>
    /// A source-generated serializer context cannot see the converters this generator writes, so each one is
    /// registered with its value object's descriptor, whether or not the assembly references the JSON package, which
    /// may only be referenced by the assembly declaring the context. It is registered as a factory, so that loading the
    /// assembly builds no converter. An assembly referencing the package also publishes each converter to the package's
    /// own registry, the only one a package older than the generator reads.
    /// </summary>
    /// <param name="referenceJsonPackage">Whether the compilation references the JSON package.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_converter_is_registered_with_its_value_object(bool referenceJsonPackage)
    {
        var run = GeneratorHarness.Run(
            """
            [ValueObject<string>]
            public readonly partial struct Code;

            [EntityId("acc")]
            public readonly partial struct AccountId;
            """,
            referenceJsonPackage: referenceJsonPackage);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        var registration = run.Files.Single(file => file.HintName == "ValueObjectRegistration.g.cs").Text;
        registration.Should()
            .Contain("Register<global::Test.Code, global::System.String>(global::Test.Code.Schema, static () => new global::Test.Code.ValueJsonConverter());")
            .And.Contain("Register<global::Test.AccountId, global::System.String>(global::Test.AccountId.Schema, static () => new global::Test.AccountId.ValueJsonConverter());");

        if (referenceJsonPackage)
        {
            registration.Should()
                .Contain("global::AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry.Register(new global::Test.Code.ValueJsonConverter());")
                .And.Contain("global::AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry.Register(new global::Test.AccountId.ValueJsonConverter());");
        }
        else
        {
            registration.Should().NotContain("ValueObjectJsonRegistry");
        }
    }

    /// <summary>
    /// The package's own registry takes a converter of a type the registration can name, which neither a generic value
    /// object, of which it knows no construction, nor a private one is: they reach an older package through no registry,
    /// as they reached none before.
    /// </summary>
    [Fact]
    public void A_generic_or_private_value_object_publishes_no_converter_to_the_package_registry()
    {
        var run = GeneratorHarness.Run(
            """
            [ValueObject<string>]
            public readonly partial struct Code<T>;

            public partial class Outer
            {
                [ValueObject<string>]
                private readonly partial struct Hidden;
            }
            """,
            referenceJsonPackage: true);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Single(file => file.HintName == "ValueObjectRegistration.g.cs").Text.Should().NotContain("ValueObjectJsonRegistry");
    }

    [Fact]
    public void The_summary_of_a_value_object_becomes_its_schema_description()
    {
        var run = GeneratorHarness.Run("""
            /// <summary>
            ///   An order reference,
            ///   as printed on the invoice.
            /// </summary>
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"An order reference, as printed on the invoice.\",");
    }

    /// <summary>
    /// A project that produces no documentation file compiles its comments as plain trivia, and most projects do:
    /// the summary is read from the trivia then.
    /// </summary>
    [Fact]
    public void The_summary_is_read_even_when_the_project_produces_no_documentation_file()
    {
        var run = GeneratorHarness.Run(
            """
            /// <summary>An order reference.</summary>
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """,
            DocumentationMode.None);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"An order reference.\",");
    }

    /// <summary>
    /// A summary is XML, and the schema's description is text for people: a reference reads as the name it refers to,
    /// a keyword as itself, and any other element as its text, whether the compiler resolved the references or the
    /// trivia holds them as written.
    /// </summary>
    /// <param name="mode">How the project compiles its documentation comments.</param>
    [Theory]
    [InlineData(DocumentationMode.Parse)]
    [InlineData(DocumentationMode.Diagnose)]
    [InlineData(DocumentationMode.None)]
    public void A_summary_is_published_as_the_text_it_reads_as(DocumentationMode mode)
    {
        var run = GeneratorHarness.Run(
            """
            /// <summary>
            /// The reference of an <see cref="Order"/>, printed by <see cref="Printer.Print(string)"/> as
            /// <c>ORD-</c>digits, never <see langword="null"/>: see <see href="https://example.com/orders">the
            /// rules</see>, a <see cref="System.Collections.Generic.List{T}"/> and its
            /// <typeparamref name="T"/> &amp; <paramref name="value"/>.
            /// <para>Case-insensitive.</para>
            /// </summary>
            [ValueObject<string>]
            public readonly partial struct OrderReference;

            public sealed class Order;

            public static class Printer
            {
                public static void Print(string value) { }
            }
            """,
            mode);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(
            "Description = \"The reference of an Order, printed by Print as ORD-digits, never null: see the rules, a List "
            + "and its T & value. Case-insensitive.\",");
    }

    /// <summary>
    /// A reference reads as what it refers to, the same whether the project produces its documentation file, and the
    /// compiler resolves the reference, or not: a member of a generic type as the member, a special type as its
    /// keyword, a constructor as its type, an operator and an indexer as they are written, a reference with no text as
    /// what it refers to, a link with no text as its address.
    /// </summary>
    /// <param name="mode">How the project compiles its documentation comments.</param>
    [Theory]
    [InlineData(DocumentationMode.Diagnose)]
    [InlineData(DocumentationMode.Parse)]
    [InlineData(DocumentationMode.None)]
    public void A_reference_in_a_summary_reads_as_what_it_refers_to(DocumentationMode mode)
    {
        var run = GeneratorHarness.Run(
            """
            /// <summary>
            /// Counted as <see cref="System.Collections.Generic.List{T}.Count"/> and <see cref="Holder{T}.Inner"/>, in a
            /// <see cref="string"/> of <see cref="int"/> digits, built by <see cref="Ledger.Ledger(string)"/> and
            /// <see cref="Ledger.operator +(Ledger, Ledger)"/>, read by <see cref="Ledger.this[int]"/>, turned into
            /// <see cref="Ledger.implicit operator int(Ledger)"/>; see <see cref="Ledger"></see>,
            /// <seealso cref="Ledger">the ledger</seealso>, <see href="https://example.com/ledger"/> and
            /// <see href="https://example.com/rules"></see>; also <see cref="Ledger.this[string[]]"/>,
            /// <see cref="Ledger.explicit operator string(Ledger)"/> and <see cref="global::System.Object"/>.
            /// </summary>
            [ValueObject<string>]
            public readonly partial struct Entry;

            public sealed class Holder<T>
            {
                public sealed class Inner;
            }

            public sealed class Ledger
            {
                public Ledger(string name) { }

                public int this[int index] => index;

                public static Ledger operator +(Ledger left, Ledger right) => left;

                public static implicit operator int(Ledger ledger) => 0;

                public int this[string[] keys] => keys.Length;

                public static explicit operator string(Ledger ledger) => string.Empty;
            }
            """,
            mode);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(
            "Description = \"Counted as Count and Inner, in a string of int digits, built by Ledger and operator +, read by "
            + "this[int], turned into implicit operator int; see Ledger, the ledger, https://example.com/ledger and "
            + "https://example.com/rules; also this[string[]], explicit operator string and object.\",");
    }

    /// <summary>
    /// A block of a summary - a line break, a list and its parts, code - separates the words around it.
    /// </summary>
    [Fact]
    public void A_block_of_a_summary_separates_words()
    {
        var run = GeneratorHarness.Run(
            """
            /// <summary>
            /// One<br/>two<list type="bullet"><listheader><term>head</term></listheader><item><term>three</term><description>four</description></item></list><code>five</code>six.
            /// </summary>
            [ValueObject<string>]
            public readonly partial struct Blocks;
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"One two head three four five six.\",");
    }

    /// <summary>
    /// The compiler reads a <c>/** */</c> comment as documentation as it reads <c>///</c> lines, and so does the
    /// generator when the project produces no documentation file, with the asterisks starting its lines left out.
    /// </summary>
    /// <param name="mode">How the project compiles its documentation comments.</param>
    [Theory]
    [InlineData(DocumentationMode.Parse)]
    [InlineData(DocumentationMode.None)]
    public void A_delimited_documentation_comment_gives_its_summary(DocumentationMode mode)
    {
        var run = GeneratorHarness.Run(
            """
            /**
             * <summary>An order reference,
             * as printed on the invoice.</summary>
             */
            [ValueObject<string>]
            public readonly partial struct OrderReference;

            /** <summary>A single-line one.</summary> */
            [ValueObject<string>]
            public readonly partial struct InvoiceNumber;

            /* <summary>An ordinary comment, which documents nothing.</summary> */
            [ValueObject<string>]
            public readonly partial struct CreditNote;

            /*** <summary>A banner, which the compiler does not read either.</summary> ***/
            [ValueObject<string>]
            public readonly partial struct Banner;

            /**/
            //// <summary>A comment out of four slashes, which documents nothing.</summary>
            [ValueObject<string>]
            public readonly partial struct Quiet;
            """,
            mode);

        run.Diagnostics.Should().BeEmpty();
        Generated("OrderReference").Should().Contain("Description = \"An order reference, as printed on the invoice.\",");
        Generated("InvoiceNumber").Should().Contain("Description = \"A single-line one.\",");
        Generated("CreditNote").Should().NotContain("Description =");
        Generated("Banner").Should().NotContain("Description =");
        Generated("Quiet").Should().NotContain("Description =");

        string Generated(string name) => run.Files.Single(file => file.HintName == HintNames.For($"Test.{name}")).Text;
    }

    /// <summary>
    /// Trivia a project producing no documentation file never has checked may hold a summary that is no XML. It loses
    /// its tags rather than its description.
    /// </summary>
    [Fact]
    public void A_summary_that_is_no_XML_loses_its_tags()
    {
        var run = GeneratorHarness.Run(
            """
            /// <summary>An <b>order</i> reference &amp; more.</summary>
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """,
            DocumentationMode.None);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"An order reference & more.\",");
    }

    [Theory]
    [InlineData("/// <remarks>Only remarks.</remarks>", DocumentationMode.Parse)]
    [InlineData("/// <remarks>Only remarks.</remarks>", DocumentationMode.None)]
    [InlineData("/// <summary>Never closed.", DocumentationMode.Parse)]
    [InlineData("/// <summary>Never closed.", DocumentationMode.None)]
    [InlineData("/// </summary>Closed first.<summary>", DocumentationMode.None)]
    [InlineData("/// <summary>   </summary>", DocumentationMode.Parse)]
    [InlineData("/// <summary>   </summary>", DocumentationMode.None)]
    public void A_doc_comment_without_a_usable_summary_publishes_no_description(string comment, DocumentationMode mode)
    {
        var run = GeneratorHarness.Run(
            $$"""
            {{comment}}
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """,
            mode);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("Description =");
    }

    [Fact]
    public void A_declared_description_wins_over_the_summary()
    {
        var run = GeneratorHarness.Run("""
            /// <summary>From the summary.</summary>
            [ValueObject<string>(Description = "From the attribute.")]
            public readonly partial struct OrderReference;
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"From the attribute.\",").And.NotContain("From the summary.");
    }

    [Fact]
    public void A_blank_text_option_is_treated_as_absent()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Description = " ", Example = "", SchemaFormat = "\t")]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("Description =").And.NotContain("Example =").And.NotContain("Format =");
    }

    /// <summary>
    /// A duration is written in the invariant constant form, <c>01:30:00</c>, and JSON Schema's <c>duration</c> is ISO
    /// 8601, <c>PT1H30M</c>, which the type does not read: a client trusting that format would send what the server
    /// refuses. A duration therefore has no natural format, and keeps one the type declares.
    /// </summary>
    [Fact]
    public void A_duration_has_no_natural_format_and_keeps_the_one_it_declares()
    {
        var bare = GeneratorHarness.Run("""
            [ValueObject<System.TimeSpan>]
            public readonly partial struct Delay;
            """);
        var declared = GeneratorHarness.Run("""
            [ValueObject<System.TimeSpan>(SchemaFormat = "constant-time-span")]
            public readonly partial struct Delay;
            """);

        bare.CompilationDiagnostics.Should().BeEmpty();
        bare.SingleValueObject.Should().NotContain("Format =").And.NotContain("duration");
        declared.CompilationDiagnostics.Should().BeEmpty();
        declared.SingleValueObject.Should().Contain("Format = \"constant-time-span\",");
    }

    /// <summary>
    /// A blank pattern is no blank text option: <c>" "</c> is a regular expression, matching any text that holds a
    /// space, and it validates and is published as written. An empty one matches everything, and is absent.
    /// </summary>
    [Fact]
    public void A_blank_pattern_is_kept_as_written_and_an_empty_one_is_absent()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0021 // The deprecated option is what this test declares.
            [ValueObject<string>(Pattern = " ")]
            public readonly partial struct Spaced;

            [ValueObject<string>(Pattern = "")]
            public readonly partial struct Unchecked;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        Generated("Spaced").Should().Contain("Pattern = \" \",").And.Contain("DeclaredPattern.IsMatch(value)");
        Generated("Unchecked").Should().NotContain("Pattern =").And.NotContain("DeclaredPattern");

        string Generated(string name) => run.Files.Single(file => file.HintName == HintNames.For($"Test.{name}")).Text;
    }

    /// <summary>The supported underlying types, by the name the diagnostics give them.</summary>
    public static TheoryData<string> SupportedUnderlyingTypes => [.. UnderlyingType.SupportedNames];

    /// <summary>
    /// Every supported type compiles, and is written to JSON in the form its kind calls for: a number as a number,
    /// a narrow integer widened to one, and a 128-bit integer, a date or a time as text read back by a helper.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedUnderlyingTypes))]
    public void Every_supported_underlying_type_produces_compiling_code(string underlying)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("'{0}' must generate code that compiles", underlying);
        run.SingleValueObject.Should().Contain(JsonForms[underlying]);
    }

    /// <summary>
    /// A null provider stands for the invariant culture, whose group separator is the comma a decimal comma writes. A
    /// real is read there with its own number styles but the group separator, and a culture that is not the invariant one
    /// keeps them, which the bridge decides; an integer refuses the separator in its own styles already. The members
    /// taking a provider say so, in documentation a project producing its documentation file resolves.
    /// </summary>
    /// <param name="underlying">The underlying type.</param>
    /// <param name="qualified">Its qualified name.</param>
    /// <param name="styles">The styles of the invariant culture, or <see langword="null"/> for the type's own.</param>
    [Theory]
    [InlineData("decimal", "global::System.Decimal", "global::System.Globalization.NumberStyles.Number & ~global::System.Globalization.NumberStyles.AllowThousands")]
    [InlineData("double", "global::System.Double", "global::System.Globalization.NumberStyles.Float")]
    [InlineData("float", "global::System.Single", "global::System.Globalization.NumberStyles.Float")]
    [InlineData("int", "global::System.Int32", null)]
    [InlineData("System.DateOnly", "global::System.DateOnly", null)]
    public void A_real_takes_no_group_separator_in_the_invariant_culture(string underlying, string qualified, string? styles)
    {
        var run = GeneratorHarness.Run(
            $$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var generated = run.SingleValueObject;
        generated.Should().Contain(styles is null
            ? $"UnderlyingValue.TryParse<{qualified}>(s, provider ?? global::System.Globalization.CultureInfo.InvariantCulture, out raw)"
            : $"UnderlyingValue.TryParse<{qualified}>(s, {styles}, provider, out raw)");
        generated.Should().Contain(
            "/// A <see langword=\"null\"/> provider stands for <see cref=\"global::System.Globalization.CultureInfo.InvariantCulture\"/>,",
            Exactly.Times(5),
            "each Parse and TryParse taking a provider says what a null one stands for");
        if (styles is null)
        {
            generated.Should().NotContain("group separator");
        }
        else
        {
            generated.Should().Contain("the text takes no group separator", Exactly.Times(5));
        }
    }

    /// <summary>A string, a bool or a char reads no culture: the members taking a provider say nothing of one.</summary>
    /// <param name="underlying">The underlying type.</param>
    [Theory]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("char")]
    public void A_value_object_reading_no_culture_says_nothing_of_the_provider(string underlying)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("provider stands for");
    }

    /// <summary>The line of the JSON converter that sets each underlying type apart.</summary>
    private static readonly Dictionary<string, string> JsonForms = new(StringComparer.Ordinal)
    {
        ["string"] = "writer.WriteStringValue(value.Value);",
        ["System.Guid"] = "writer.WriteStringValue(value.Value);",
        ["bool"] = "writer.WriteBooleanValue(value.Value);",
        ["char"] = "buffer[0] = value.Value;",
        ["sbyte"] = "writer.WriteNumberValue((int)value.Value);",
        ["byte"] = "writer.WriteNumberValue((int)value.Value);",
        ["short"] = "writer.WriteNumberValue((int)value.Value);",
        ["ushort"] = "writer.WriteNumberValue((int)value.Value);",
        ["int"] = "writer.WriteNumberValue(value.Value);",
        ["uint"] = "writer.WriteNumberValue(value.Value);",
        ["long"] = "writer.WriteNumberValue(value.Value);",
        ["ulong"] = "writer.WriteNumberValue(value.Value);",
        ["System.Int128"] = "private static global::System.Int128 ReadInt128(ref",
        ["System.UInt128"] = "private static global::System.UInt128 ReadUInt128(ref",
        ["decimal"] = "writer.WriteNumberValue(value.Value);",
        ["double"] = "writer.WriteNumberValue(value.Value);",
        ["float"] = "writer.WriteNumberValue(value.Value);",
        ["System.DateOnly"] = "private static global::System.DateOnly ReadDateOnly(ref",
        ["System.TimeOnly"] = "private static global::System.TimeOnly ReadTimeOnly(ref",
        ["System.DateTime"] = "writer.WriteStringValue(value.Value);",
        ["System.DateTimeOffset"] = "writer.WriteStringValue(value.Value);",
        ["System.TimeSpan"] = "private static global::System.TimeSpan ReadTimeSpan(ref",
    };

    /// <summary>
    /// The narrow integers promote to int under arithmetic, so their results are cast back; every integral
    /// operation is checked; and an unsigned type has no negation, and is its own absolute value.
    /// </summary>
    [Theory]
    [InlineData("sbyte", "checked((sbyte)(left.Value + right.Value))", true)]
    [InlineData("byte", "checked((byte)(left.Value + right.Value))", false)]
    [InlineData("short", "checked((short)(left.Value + right.Value))", true)]
    [InlineData("ushort", "checked((ushort)(left.Value + right.Value))", false)]
    [InlineData("int", "checked(left.Value + right.Value)", true)]
    [InlineData("uint", "checked(left.Value + right.Value)", false)]
    [InlineData("long", "checked(left.Value + right.Value)", true)]
    [InlineData("ulong", "checked(left.Value + right.Value)", false)]
    [InlineData("Int128", "checked(left.Value + right.Value)", true)]
    [InlineData("UInt128", "checked(left.Value + right.Value)", false)]
    [InlineData("decimal", "left.Value + right.Value", true)]
    [InlineData("double", "left.Value + right.Value", true)]
    [InlineData("float", "left.Value + right.Value", true)]
    public void Arithmetic_compiles_for_every_numeric_underlying_type(string underlying, string sum, bool negatable)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Arithmetic = true)]
            public readonly partial struct Count;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("arithmetic over '{0}' must compile", underlying);

        var generated = run.SingleValueObject;
        generated.Should().Contain($"operator +(global::Test.Count left, global::Test.Count right) => Create({sum});");
        generated.Contains("operator -(global::Test.Count value)", StringComparison.Ordinal)
            .Should().Be(negatable, "only a signed type is negated");
        generated.Contains("Abs(global::Test.Count value) => value;", StringComparison.Ordinal)
            .Should().Be(!negatable, "an unsigned value is its own absolute value");
    }

    /// <summary>
    /// A consumer's compilation runs the framework's regex generator beside this one, and so does the harness: a
    /// <c>[GeneratedRegex]</c> partial property gets its other half written, as it would in a real project.
    /// </summary>
    [Fact]
    public void A_snippet_compiles_a_source_generated_regex_as_a_consumer_project_does()
    {
        var run = GeneratorHarness.Run("""
            public static partial class Shapes
            {
                [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Currency { get; }
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("the regex generator writes the other half of the property");
    }

    /// <summary>
    /// Static initializers run in declaration order, and a named constant goes through <c>Create</c>, and so through
    /// the compiled pattern, while it is created. The pattern therefore has to be declared first, or the type
    /// initializer meets a null field and the module initializer takes the whole assembly down with it.
    /// </summary>
    [Fact]
    public void The_compiled_pattern_is_declared_before_the_named_constants_that_go_through_it()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0021 // The deprecated option is what this test declares.
            [ValueObject<string>(Pattern = "^[A-Z]{3}$")]
            [KnownValue("Euro", "EUR")]
            public readonly partial struct CurrencyCode;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var generated = run.SingleValueObject;
        var pattern = generated.IndexOf("Regex DeclaredPattern = new(", StringComparison.Ordinal);
        var constant = generated.IndexOf("CurrencyCode Euro { get; } = Create(", StringComparison.Ordinal);
        pattern.Should().BePositive();
        constant.Should().BePositive();
        pattern.Should().BeLessThan(constant, "the constant goes through the pattern while the type initializes");
    }

    /// <summary>
    /// The type converter of a numeric value object takes every numeric type a value object may wrap, its own through
    /// <c>Create</c> and each other through a typed arm and a checked bridge, chosen by the type converted to: no
    /// reflection, and nothing truncated. It hands its value to each of them the same way.
    /// </summary>
    /// <param name="underlying">The underlying type, as the attribute names it.</param>
    /// <param name="value">Its qualified name.</param>
    /// <param name="bridge">The bridge that converts a number to it.</param>
    [Theory]
    [InlineData("int", "global::System.Int32", "TryConvertToInteger")]
    [InlineData("byte", "global::System.Byte", "TryConvertToInteger")]
    [InlineData("System.UInt128", "global::System.UInt128", "TryConvertToInteger")]
    [InlineData("decimal", "global::System.Decimal", "TryConvertToReal")]
    [InlineData("double", "global::System.Double", "TryConvertToReal")]
    [InlineData("float", "global::System.Single", "TryConvertToReal")]
    public void A_numeric_type_converter_converts_every_number_through_a_checked_bridge(string underlying, string value, string bridge)
    {
        var run = GeneratorHarness.Run(
            $$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Measure;
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var converter = TypeConverterOf(run.SingleValueObject);
        converter.Should().Contain("Converts <see cref=\"Measure\"/> to and from text and any number.");
        converter.Should().Contain($"case {value} underlying:").And.Contain("return global::Test.Measure.Create(underlying);");
        converter.Should().Contain($"fits = global::AdCodicem.ValueObjects.UnderlyingValue.{bridge}(number, out raw);");
        converter.Should().Contain($"\"'Measure' rejected the supplied number: The number is not a valid {underlying}.\",");
        converter.Should().Contain("global::AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable,");
        foreach (var (name, real) in Numbers)
        {
            var number = "global::System." + name;
            converter.Should().Contain($"type == typeof({number})");
            if (number == value)
            {
                converter.Should().NotContain($"case {number} number:", "its own type goes straight to Create");
                continue;
            }

            converter.Should().Contain($"case {number} number:");
            converter.Should().Contain(
                $"&& global::AdCodicem.ValueObjects.UnderlyingValue.{(real ? "TryConvertToReal" : "TryConvertToInteger")}(typed.Value, out {number} to{name}))");
        }
    }

    /// <summary>
    /// A value object over anything but a number converts from and to text and its own type only: a number is no
    /// identifier, no flag and no date.
    /// </summary>
    /// <param name="underlying">The underlying type, as the attribute names it.</param>
    [Theory]
    [InlineData("string")]
    [InlineData("System.Guid")]
    [InlineData("bool")]
    [InlineData("char")]
    [InlineData("System.DateOnly")]
    [InlineData("System.TimeSpan")]
    public void A_type_converter_over_anything_but_a_number_converts_no_number(string underlying)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var converter = TypeConverterOf(run.SingleValueObject);
        converter.Should().Contain("to and from text and its underlying value.");
        converter.Should().NotContain(" number:").And.NotContain("IsNumber").And.NotContain("UnderlyingValue.TryConvert");
    }

    /// <summary>The numeric types a value object may wrap, by their name in <c>System</c>, and whether each is a real.</summary>
    private static readonly (string Name, bool Real)[] Numbers =
    [
        ("SByte", false), ("Byte", false), ("Int16", false), ("UInt16", false), ("Int32", false), ("UInt32", false),
        ("Int64", false), ("UInt64", false), ("Int128", false), ("UInt128", false), ("Decimal", true), ("Double", true),
        ("Single", true),
    ];

    /// <summary>Gets the type converter nested in a generated value object, from its summary to the end.</summary>
    private static string TypeConverterOf(string generated)
    {
        var start = generated.IndexOf("/// <summary>Converts <see cref=", StringComparison.Ordinal);
        start.Should().BePositive();

        return generated[start..];
    }

    /// <summary>
    /// A named argument written twice is the compiler's error (CS0643), which it reports while the generator still
    /// runs, in the IDE as it is typed: the generator reads the first and keeps producing every value object, rather
    /// than throwing and dropping its whole output (#136).
    /// </summary>
    [Fact]
    public void A_named_argument_written_twice_leaves_the_compiler_error_alone()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Example = "1", Example = "2")]
            public readonly partial struct Quantity;

            [EntityId("cus", Granularity = IdGranularity.Hour, Granularity = IdGranularity.Day)]
            public readonly partial struct CustomerId;

            [ValueObject<string>]
            public readonly partial struct Sku;
            """);

        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("CS0643", "CS0643");
        run.Files.Select(file => file.HintName).Should()
            .Contain(name => name.Contains("Quantity", StringComparison.Ordinal))
            .And.Contain(name => name.Contains("CustomerId", StringComparison.Ordinal))
            .And.Contain(name => name.Contains("Sku", StringComparison.Ordinal));
    }
}
