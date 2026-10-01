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
/// cannot edit.
/// </remarks>
public sealed class KnownValueNameTests
{
    [Theory]
    [InlineData("class")]
    [InlineData("default")]
    [InlineData("event")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("int")]
    [InlineData("__arglist")]
    public void A_known_value_named_after_a_keyword_is_reported_and_the_others_are_generated(string name)
        => AssertRefusedAlone(name);

    /// <summary>
    /// None of these is a member the generator writes, but a static property of that name would still break the
    /// generated code: <c>_</c> would capture the discard it writes as <c>out _</c>, and the others would hide a
    /// member of <see cref="object"/>, with a warning inside the generated file.
    /// </summary>
    [Theory]
    [InlineData("_")]
    [InlineData("GetType")]
    [InlineData("MemberwiseClone")]
    [InlineData("ReferenceEquals")]
    public void A_known_value_named_after_the_discard_or_an_inherited_member_is_reported(string name)
        => AssertRefusedAlone(name);

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
    /// and forgotten by the check fails here. Each configuration turns on every option that adds a member.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>(Pattern = "^[A-Z]+$", ValueSet = ValueSetKind.Closed)]
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
        [ValueObject<int>(Arithmetic = true, ValueSet = ValueSetKind.Closed)]
        [KnownValue("Kept", 1)]
        public readonly partial struct Code;
        """,
        "1")]
    public void Every_member_the_generator_writes_is_refused_as_a_known_value_name(string declaration, string literal)
    {
        var members = MembersWrittenOn(GeneratorHarness.Run(declaration).SingleValueObject, "Code")
            .Where(name => name != "Kept")
            .ToList();

        members.Should().Contain(["Code", "Value", "Schema", "Create", "KnownValues"], "the reading must keep finding them");

        var attributes = string.Concat(members.Select(name => $"[KnownValue(\"{name}\", {literal})]\n"));
        var run = GeneratorHarness.Run(attributes + declaration);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0006");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            members.Select(name => $"'{name}' is not usable as the name of a generated member on 'Code'"));
        MembersWrittenOn(run.SingleValueObject, "Code").Should().BeEquivalentTo([.. members, "Kept"]);
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The members follow the options, and so do the names they take: without arithmetic there is no <c>Zero</c>
    /// to collide with.
    /// </summary>
    [Theory]
    [InlineData("int", "Zero", "0")]
    [InlineData("int", "One", "1")]
    [InlineData("int", "Sum", "2")]
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
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'?' is not usable as the name of a generated member on 'Country'");
        run.CompilationDiagnostics.Should().OnlyContain(
            compiled => compiled.Id == "CS8625",
            "the null literal is the author's to answer for, and nothing generated may add to it");
    }

    /// <summary>
    /// Asserts that a known value of that name is reported, on the declaration, while a second one is generated
    /// and the generated code compiles.
    /// </summary>
    private static void AssertRefusedAlone(string name)
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
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'{name}' is not usable as the name of a generated member on 'Country'");

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
    /// The name a member takes in the scope of the type. Operators and explicit interface implementations take
    /// none, and a constructor takes the name of the type.
    /// </summary>
    private static IEnumerable<string> NamesOf(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
        PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: null } property => [property.Identifier.ValueText],
        MethodDeclarationSyntax { ExplicitInterfaceSpecifier: null } method => [method.Identifier.ValueText],
        ConstructorDeclarationSyntax constructor => [constructor.Identifier.ValueText],
        BaseTypeDeclarationSyntax nested => [nested.Identifier.ValueText],
        _ => [],
    };
}
