using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AdCodicem.ValueObjects.GeneratorTests.Harness;

/// <summary>
/// Reads the names the generated members of a value object take in its scope, out of the generated code itself.
/// </summary>
/// <remarks>
/// Reading them rather than listing them is the point: a member added to an emitter and forgotten by the names the
/// generator refuses fails the tests that compare the two.
/// </remarks>
public static class GeneratedMembers
{
    /// <summary>
    /// Gets the names the members of a generated type take in its scope.
    /// </summary>
    /// <param name="generated">The generated source.</param>
    /// <param name="typeName">The name of the value object it declares.</param>
    /// <returns>Each name once, in the order the members are written.</returns>
    public static List<string> WrittenOn(string generated, string typeName)
    {
        var type = CSharpSyntaxTree.ParseText(generated, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == typeName);

        return [.. type.Members.SelectMany(NamesOf).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The names a member takes in the scope of the type. An operator takes its metadata name, a property its own and
    /// its getter's, explicit interface implementations take none, and a constructor takes the name of the type.
    /// </summary>
    private static IEnumerable<string> NamesOf(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
        PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: null } property => NamesOf(property),
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
    /// A property's name, and its getter's, which the generator always writes: every generated property is read-only.
    /// </summary>
    private static IEnumerable<string> NamesOf(PropertyDeclarationSyntax property)
    {
        property.AccessorList?.Accessors.Should().OnlyContain(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));

        return [property.Identifier.ValueText, $"get_{property.Identifier.ValueText}"];
    }

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
}
