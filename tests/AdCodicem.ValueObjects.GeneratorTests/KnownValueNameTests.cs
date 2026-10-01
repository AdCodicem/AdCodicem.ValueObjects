using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The names a known value may take, and the ones <c>VO0006</c> refuses.
/// </summary>
/// <remarks>
/// The first argument of <c>[KnownValue]</c> becomes a static property of the value object. A name the C# compiler
/// would not accept there must be reported by the generator, or the author is handed an error inside a file they
/// cannot edit. The message ends with the rule the name broke.
/// </remarks>
public sealed class KnownValueNameTests
{
    private const string NotAnIdentifier = "it is not a C# identifier";
    private const string Keyword = "it is a C# keyword";
    private const string Generated = "the generated code already uses that name";
    private const string Declared = "the type already has a member of that name";
    private const string Duplicate = "another known value already takes that name";

    [Theory]
    [InlineData("class")]
    [InlineData("default")]
    [InlineData("event")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("int")]
    [InlineData("__arglist")]
    public void A_known_value_named_after_a_keyword_is_reported_and_the_others_are_generated(string name)
        => AssertRefusedAlone(name, Keyword);

    [Theory]
    [InlineData("not an identifier")]
    [InlineData("1st")]
    [InlineData("Fr-Be")]
    [InlineData("")]
    public void A_known_value_whose_name_is_not_an_identifier_is_reported(string name)
        => AssertRefusedAlone(name, NotAnIdentifier);

    /// <summary>
    /// None of these is a member the generator writes, but a static property of that name would still break the
    /// generated code: <c>_</c> would capture the discard it writes as <c>out _</c>, and the others would hide a
    /// member of <see cref="object"/>, with a warning inside the generated file.
    /// </summary>
    [Theory]
    [InlineData("_", Generated)]
    [InlineData("GetType", Declared)]
    [InlineData("MemberwiseClone", Declared)]
    [InlineData("ReferenceEquals", Declared)]
    public void A_known_value_named_after_the_discard_or_an_inherited_member_is_reported(string name, string reason)
        => AssertRefusedAlone(name, reason);

    /// <summary>
    /// The author's own members are in the type the generator reopens, and a static property of the same name would
    /// collide with each of them in the generated file, whatever its kind: a hook, a field, a property, a nested
    /// type.
    /// </summary>
    [Fact]
    public void A_known_value_named_after_a_member_the_type_declares_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("NormalizeValue", "N")]
            [KnownValue("Fallback", "B")]
            [KnownValue("Region", "R")]
            [KnownValue("Formats", "S")]
            [KnownValue("France", "FR")]
            public readonly partial struct Country : IValueObjectNormalizer<string>
            {
                public static readonly string Fallback = "FR";

                public string Region => Value[..1];

                public static string NormalizeValue(string value) => value.ToUpperInvariant();

                public static class Formats
                {
                    public const string Short = "S";
                }
            }
            """);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0006");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("NormalizeValue", "Country", Declared),
            Message("Fallback", "Country", Declared),
            Message("Region", "Country", Declared),
            Message("Formats", "Country", Declared));
        run.SingleValueObject.Should().Contain("public static global::Test.Country France { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_second_known_value_of_the_same_name_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR")]
            [KnownValue("France", "FX")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("France", "Country", Duplicate));
        run.SingleValueObject.Should().Contain("public static global::Test.Country France { get; } = Create(\"FR\");");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A contextual keyword is an ordinary identifier outside the construct that gives it meaning, so refusing one
    /// would refuse a name the compiler accepts.
    /// </summary>
    [Theory]
    [InlineData("var")]
    [InlineData("value")]
    [InlineData("record")]
    [InlineData("async")]
    [InlineData("await")]
    [InlineData("nameof")]
    [InlineData("field")]
    [InlineData("dynamic")]
    [InlineData("when")]
    public void A_known_value_named_after_a_contextual_keyword_is_generated(string name)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>]
            [KnownValue("{{name}}", "K")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"public static global::Test.Country {name} {{ get; }}");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Reads the members out of the generated code rather than out of a list, so that a member added to an emitter
    /// and forgotten by the check fails here. An operator counts under its metadata name, which the compiler reserves
    /// in the type as it does any other member's. Each configuration turns on every option that adds a member.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>(Pattern = "^[A-Z]+$", ValueSet = ValueSetKind.Closed, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
        [KnownValue("Kept", "K")]
        public readonly partial struct Code : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer
        {
            public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

            public static string NormalizeValue(ReadOnlySpan<char> value) => value.Trim().ToString();
        }
        """,
        "\"K\"")]
    [InlineData(
        """
        [ValueObject<int>(Arithmetic = true, ValueSet = ValueSetKind.Closed, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
        [KnownValue("Kept", 1)]
        public readonly partial struct Code;
        """,
        "1")]
    public void Every_member_the_generator_writes_is_refused_as_a_known_value_name(string declaration, string literal)
    {
        var members = MembersWrittenOn(GeneratorHarness.Run(declaration).SingleValueObject, "Code")
            .Where(name => name != "Kept")
            .ToList();

        members.Should().Contain(["Code", "Value", "Schema", "Create", "KnownValues", "op_Equality", "op_Implicit"], "the reading must keep finding them");

        var attributes = string.Concat(members.Select(name => $"[KnownValue(\"{name}\", {literal})]\n"));
        var run = GeneratorHarness.Run(attributes + declaration);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0006");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            members.Select(name => Message(name, "Code", Generated)));
        MembersWrittenOn(run.SingleValueObject, "Code").Should().BeEquivalentTo([.. members, "Kept"]);
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The members follow the options, and so do the names they take: without arithmetic there is no <c>Zero</c>
    /// to collide with, nor an addition operator, and without a conversion no conversion operator.
    /// </summary>
    [Theory]
    [InlineData("int", "Zero", "0")]
    [InlineData("int", "One", "1")]
    [InlineData("int", "Sum", "2")]
    [InlineData("int", "op_Addition", "3")]
    [InlineData("int", "op_Implicit", "4")]
    [InlineData("int", "op_Explicit", "5")]
    [InlineData("string", "DeclaredPattern", "\"P\"")]
    [InlineData("string", "KnownUnderlyingValues", "\"K\"")]
    [InlineData("string", "TryCreateFrom", "\"T\"")]
    public void A_member_name_the_options_leave_free_is_usable_as_a_known_value_name(
        string underlying,
        string name,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("{{name}}", {{literal}})]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"public static global::Test.Code {name} {{ get; }}");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_known_value_without_a_name_is_reported_under_a_question_mark()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue(null, "FR")]
            public readonly partial struct Country;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0006");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Country", "public readonly partial struct Country;"));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(Message("?", "Country", NotAnIdentifier));
        run.CompilationDiagnostics.Should().OnlyContain(
            compiled => compiled.Id == "CS8625",
            "the null literal is the author's to answer for, and nothing generated may add to it");
    }

    /// <summary>
    /// A negation is written only over a signed type, so its name stays free on an unsigned one.
    /// </summary>
    [Fact]
    public void The_negation_operator_takes_its_name_only_where_it_is_written()
    {
        var signed = GeneratorHarness.Run("""
            [ValueObject<int>(Arithmetic = true)]
            [KnownValue("op_UnaryNegation", 1)]
            public readonly partial struct Code;
            """);
        var unsigned = GeneratorHarness.Run("""
            [ValueObject<uint>(Arithmetic = true)]
            [KnownValue("op_UnaryNegation", 1u)]
            public readonly partial struct Code;
            """);

        signed.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("op_UnaryNegation", "Code", Generated));
        signed.CompilationDiagnostics.Should().BeEmpty();
        unsigned.Diagnostics.Should().BeEmpty();
        unsigned.SingleValueObject.Should().Contain("public static global::Test.Code op_UnaryNegation { get; }");
        unsigned.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Asserts that a known value of that name is reported, on the declaration and with the rule it broke, while a
    /// second one is generated and the generated code compiles.
    /// </summary>
    private static void AssertRefusedAlone(string name, string reason)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>]
            [KnownValue("{{name}}", "K")]
            [KnownValue("France", "FR")]
            public readonly partial struct Country;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0006");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Country", "public readonly partial struct Country;"));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(Message(name, "Country", reason));

        run.SingleValueObject.Should().Contain("public static global::Test.Country France { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    private static List<string> MembersWrittenOn(string generated, string typeName)
    {
        var type = CSharpSyntaxTree.ParseText(generated, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == typeName);

        return [.. type.Members.SelectMany(NamesOf).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The name a member takes in the scope of the type. An operator takes its metadata name, explicit interface
    /// implementations take none, and a constructor takes the name of the type.
    /// </summary>
    private static IEnumerable<string> NamesOf(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
        PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: null } property => [property.Identifier.ValueText],
        MethodDeclarationSyntax { ExplicitInterfaceSpecifier: null } method => [method.Identifier.ValueText],
        ConstructorDeclarationSyntax constructor => [constructor.Identifier.ValueText],
        BaseTypeDeclarationSyntax nested => [nested.Identifier.ValueText],
        OperatorDeclarationSyntax { ExplicitInterfaceSpecifier: null } @operator => [MetadataNameOf(@operator)],
        ConversionOperatorDeclarationSyntax { ExplicitInterfaceSpecifier: null } conversion =>
            [conversion.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ImplicitKeyword)
                ? WellKnownMemberNames.ImplicitConversionName
                : WellKnownMemberNames.ExplicitConversionName],
        _ => [],
    };

    /// <summary>
    /// The metadata name of an operator the generator writes. One it does not write yet fails the test, so that the
    /// operator is added here, and to the names the generator refuses, when it is added to an emitter.
    /// </summary>
    private static string MetadataNameOf(OperatorDeclarationSyntax @operator)
        => (@operator.OperatorToken.Kind(), @operator.ParameterList.Parameters.Count) switch
        {
            (SyntaxKind.EqualsEqualsToken, 2) => WellKnownMemberNames.EqualityOperatorName,
            (SyntaxKind.ExclamationEqualsToken, 2) => WellKnownMemberNames.InequalityOperatorName,
            (SyntaxKind.LessThanToken, 2) => WellKnownMemberNames.LessThanOperatorName,
            (SyntaxKind.GreaterThanToken, 2) => WellKnownMemberNames.GreaterThanOperatorName,
            (SyntaxKind.LessThanEqualsToken, 2) => WellKnownMemberNames.LessThanOrEqualOperatorName,
            (SyntaxKind.GreaterThanEqualsToken, 2) => WellKnownMemberNames.GreaterThanOrEqualOperatorName,
            (SyntaxKind.PlusToken, 2) => WellKnownMemberNames.AdditionOperatorName,
            (SyntaxKind.MinusToken, 2) => WellKnownMemberNames.SubtractionOperatorName,
            (SyntaxKind.MinusToken, 1) => WellKnownMemberNames.UnaryNegationOperatorName,
            (SyntaxKind.AsteriskToken, 2) => WellKnownMemberNames.MultiplyOperatorName,
            (SyntaxKind.SlashToken, 2) => WellKnownMemberNames.DivisionOperatorName,
            var unknown => throw new InvalidOperationException($"The operator {unknown} has no metadata name here yet."),
        };

    private static string Message(string name, string typeName, string reason)
        => $"'{name}' is not usable as the name of a generated member on '{typeName}': {reason}";
}
