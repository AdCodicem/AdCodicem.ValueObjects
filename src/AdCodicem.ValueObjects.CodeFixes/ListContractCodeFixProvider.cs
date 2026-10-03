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
/// Lists the contract the generator implements on the declaration of a value object that the Request Delegate
/// Generator cannot see as parsable (<c>VO0033</c>).
/// </summary>
/// <remarks>
/// The interface goes on the declaration carrying the annotation, which is the consumer's syntax and so what the
/// Request Delegate Generator reads. It is the contract the generator implements, so the declaration states what the
/// type is and requires nothing the generator does not write: <c>IValueObject&lt;TSelf, TValue&gt;</c>,
/// <c>INumericValueObject&lt;TSelf, TValue&gt;</c> for an arithmetic value object, or <c>IEntityId&lt;TSelf&gt;</c>.
/// Its namespace is imported when the file does not import it yet.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListContractCodeFixProvider))]
[Shared]
public sealed class ListContractCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [RequestDelegateGeneratorAnalyzer.HiddenFromRequestDelegateGenerator.Id];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var declaration = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?
                .FirstAncestorOrSelf<TypeDeclarationSyntax>();

            if (declaration is null
                || semanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type
                || RequestDelegateGeneratorAnalyzer.Contract(type, semanticModel.Compilation) is not { } contract)
            {
                continue;
            }

            var title = $"List {contract.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)} on the declaration";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ListAsync(context.Document, declaration, contract, cancellationToken),
                    equivalenceKey: nameof(ListContractCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<Document> ListAsync(
        Document document,
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol contract,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Written fully qualified and annotated, so that the clean-up of the code action imports its namespace when the
        // file lacks it, then shortens it. TypeExpression(contract, addImport: true) leaves the import out.
        var listed = editor.Generator.TypeExpression(contract).WithAdditionalAnnotations(Simplifier.AddImportsAnnotation);
        editor.AddInterfaceType(declaration, listed);

        return editor.GetChangedDocument();
    }
}
