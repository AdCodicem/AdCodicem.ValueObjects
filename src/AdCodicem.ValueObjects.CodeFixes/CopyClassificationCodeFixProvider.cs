using System.Collections.Immutable;
using System.Composition;
using AdCodicem.ValueObjects.Generators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;

namespace AdCodicem.ValueObjects.CodeFixes;

/// <summary>
/// Copies the classification of a value object's type onto the parameter or the property that logs it (<c>VO0045</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every attribute that classifies the type is copied, in its declared order, so that the logging generator builds the
/// <c>DataClassificationSet</c> the type states, and the redactor the application configures for it applies. A
/// property of a record is classified on its positional parameter, which the logging generator reads as it reads the
/// property, and the namespace of each attribute is imported when the file does not import it yet. Fixing all of a
/// document classifies each declaration once, however many diagnostics name it.
/// </para>
/// <para>
/// Nothing is offered where a copy would not compile or could not be written: an attribute written with arguments, since
/// the logging generator builds a classification as <c>new TAttribute().Classification</c> whatever the attribute
/// carries; an attribute whose <c>[AttributeUsage]</c> keeps it off a parameter or a property; a member declared in
/// another assembly; and a property of a record logged through its <c>ToString()</c>, which the logging generator
/// refuses once the property is classified (<c>LOGGEN035</c>). The warning stays in each case, and its message says what
/// to write instead.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CopyClassificationCodeFixProvider))]
[Shared]
public sealed class CopyClassificationCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [DataClassificationAnalyzer.LoggedInClear.Id];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            // The declaration to classify: the parameter reported, or the member the additional location names.
            var location = diagnostic.Properties.TryGetValue(DataClassificationAnalyzer.TargetKey, out var target)
                ? target switch
                {
                    DataClassificationAnalyzer.ParameterTarget => diagnostic.Location,
                    DataClassificationAnalyzer.MemberTarget when diagnostic.AdditionalLocations.Count > 0 => diagnostic.AdditionalLocations[0],
                    _ => null,
                }
                : null;

            if (location?.SourceTree is not { } tree || context.Document.Project.Solution.GetDocument(tree) is not { } document)
            {
                continue;
            }

            // A C# document always has both.
            var root = (await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
            var semanticModel = (await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false))!;

            var declaration = root.FindToken(location.SourceSpan.Start).Parent;
            var (logged, attributeTarget) = declaration switch
            {
                ParameterSyntax parameter => (
                    DataClassificationAnalyzer.LoggedType(semanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)!.Type),
                    AttributeTargets.Parameter),
                PropertyDeclarationSyntax property => (
                    DataClassificationAnalyzer.LoggedType(semanticModel.GetDeclaredSymbol(property, context.CancellationToken)!.Type),
                    AttributeTargets.Property),
                _ => ((ITypeSymbol?)null, default(AttributeTargets)),
            };

            if (logged is null || Copyable(logged, attributeTarget) is not { IsEmpty: false } classifications)
            {
                continue;
            }

            var shown = string.Join(", ", classifications.Select(static attribute => ValueObjectAnnotations.Shown(attribute.AttributeClass!)));
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Classify with {shown}, as '{logged.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}' is",
                    cancellationToken => ClassifyAsync(document, declaration!, classifications, cancellationToken),
                    equivalenceKey: nameof(CopyClassificationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>
    /// Gets the classifications of a type the code fix copies onto a target, or none when one of them cannot be copied
    /// there: written with arguments, or kept off the target by its usage.
    /// </summary>
    private static ImmutableArray<AttributeData> Copyable(ITypeSymbol logged, AttributeTargets target)
    {
        var classifications = DataClassificationAnalyzer.Classifications(logged);

        return classifications.Any(attribute =>
            !attribute.ConstructorArguments.IsEmpty
            || !attribute.NamedArguments.IsEmpty
            || !DataClassificationAnalyzer.AllowsTarget(attribute.AttributeClass, target))
            ? []
            : classifications;
    }

    private static async Task<Solution> ClassifyAsync(
        Document document,
        SyntaxNode declaration,
        ImmutableArray<AttributeData> classifications,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        foreach (var classification in classifications)
        {
            // Written fully qualified and annotated, so that the clean-up of the code action imports its namespace when
            // the file lacks it, then shortens it.
            var attribute = editor.Generator.Attribute(classification)
                .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
            editor.AddAttribute(declaration, attribute);
        }

        return editor.GetChangedDocument().Project.Solution;
    }
}
