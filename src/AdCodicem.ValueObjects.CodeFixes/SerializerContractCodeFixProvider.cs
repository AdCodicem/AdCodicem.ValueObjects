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
/// Names protobuf-net's serializer in a <c>[ProtoContract]</c> that names none, or removes the attribute (<c>VO0043</c>,
/// <c>VO0044</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>[ProtoContract]</c> on a value object becomes the attribute route of <c>AdCodicem.ValueObjects.ProtobufNet</c>,
/// <c>[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer&lt;Sku, string&gt;))]</c>, which protobuf-net's
/// compile-time model, and so native AOT, takes: the strict profile at protobuf-net's default level, or the profile a
/// serializer copied from another value object already named, which the title says, since a model at another level or
/// a trusted registration wrote the value object otherwise. Its other arguments stay, <c>Name</c> included, and the
/// namespace of the serializer is imported when the file does not import it yet. It is offered where the compilation
/// reaches the serializer, the value object is neither generic nor nested in a generic type, and protobuf-net writes its
/// underlying type.
/// </para>
/// <para>
/// Every reported attribute can be removed, alone: from a list holding others, the others stay; a list holding it alone
/// goes, and the doc comment and the directives above it stay, handed to what follows, as does the line break of a list
/// sharing its line with what precedes it. Removing <c>[Serializable]</c> says, in its title, what
/// <c>DataContractSerializer</c> then writes. Fixing all of a document removes the attribute chosen alone, each
/// attribute having a fix of its own.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializerContractCodeFixProvider))]
[Shared]
public sealed class SerializerContractCodeFixProvider : CodeFixProvider
{
    private const string NameTitle = "Name the value object's protobuf-net serializer";

    /// <summary>
    /// <c>[Serializable]</c> as shown: the one attribute whose removal loses a value its serializer kept.
    /// </summary>
    private const string SerializableShown = "[Serializable]";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [SerializerContractAnalyzer.ContractLosesTheValue.Id, SerializerContractAnalyzer.ContractSkipsTheRules.Id];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // A C# document always has both.
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var semanticModel = (await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false))!;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute
                || semanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeClass }
                || !SerializerContractAnalyzer.IsContractAttribute(attributeClass))
            {
                continue;
            }

            if (diagnostic.Id == SerializerContractAnalyzer.ContractLosesTheValue.Id
                && Serializer(attribute, attributeClass, semanticModel, context.CancellationToken) is { } serializer)
            {
                // The title says what is written: the level and the trust are the profile's, whatever the model's were.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        serializer.TypeArguments.Length == 3
                            ? $"{NameTitle}, keeping the profile {serializer.TypeArguments[2].Name}"
                            : $"{NameTitle}, strict at Level200",
                        cancellationToken => NameAsync(context.Document, attribute, serializer, cancellationToken),
                        equivalenceKey: nameof(SerializerContractCodeFixProvider) + ".Name"),
                    diagnostic);
            }

            // Outside the XML opt-in, DataContractSerializer writes a value object without [Serializable] as an empty
            // element: its removal says so.
            var shown = ValueObjectAnnotations.Shown(attributeClass);
            context.RegisterCodeFix(
                CodeAction.Create(
                    shown == SerializableShown
                        ? $"Remove {shown}: DataContractSerializer then writes an empty element"
                        : $"Remove {shown}",
                    cancellationToken => RemoveAsync(context.Document, attribute, cancellationToken),
                    equivalenceKey: nameof(SerializerContractCodeFixProvider) + ".Remove." + shown.Trim('[', ']')),
                diagnostic);
        }
    }

    /// <summary>
    /// Gets the serializer to name in a <c>[ProtoContract]</c> on a type declaration, or <see langword="null"/> where none
    /// can be named.
    /// </summary>
    private static INamedTypeSymbol? Serializer(
        AttributeSyntax attribute,
        INamedTypeSymbol attributeClass,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // An attribute always sits in a list, whose parent is what it applies to: an enum is no type declaration here.
        if (!SerializerContractAnalyzer.IsProtoContract(attributeClass)
            || attribute.Parent!.Parent is not TypeDeclarationSyntax declaration)
        {
            return null;
        }

        var type = semanticModel.GetDeclaredSymbol(declaration, cancellationToken)!;

        // The attribute as the type carries it, for the profile of a serializer it already names; none for an attribute
        // the compiler applies to something else, [method: ProtoContract] on a type.
        var protoContract = type.GetAttributes().FirstOrDefault(candidate =>
            candidate.ApplicationSyntaxReference!.GetSyntax(cancellationToken) == attribute);

        return protoContract is null
            ? null
            : SerializerContractAnalyzer.SerializerFor(type, protoContract, semanticModel.Compilation);
    }

    private static async Task<Document> NameAsync(
        Document document,
        AttributeSyntax attribute,
        INamedTypeSymbol serializer,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Written fully qualified and annotated, so that the clean-up of the code action imports its namespace when the
        // file lacks it, then shortens it, as ListContractCodeFixProvider lists a contract.
        var typeOf = SyntaxFactory.TypeOfExpression(
            ((TypeSyntax)editor.Generator.TypeExpression(serializer)).WithAdditionalAnnotations(Simplifier.AddImportsAnnotation));

        // A Serializer already named, null or another value object's, is replaced where it stands: a second one is CS0643.
        var arguments = attribute.ArgumentList ?? SyntaxFactory.AttributeArgumentList();
        var existing = arguments.Arguments
            .Select(static argument => argument.NameEquals)
            .OfType<NameEqualsSyntax>()
            .FirstOrDefault(static name => name.Name.Identifier.ValueText == "Serializer");
        arguments = existing is null
            ? arguments.AddArguments(SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Serializer")),
                nameColon: null,
                typeOf))
            : arguments.ReplaceNode(existing.Parent!, ((AttributeArgumentSyntax)existing.Parent!).WithExpression(typeOf));

        editor.ReplaceNode(attribute, attribute.WithArgumentList(arguments));

        return editor.GetChangedDocument();
    }

    private static async Task<Document> RemoveAsync(Document document, AttributeSyntax attribute, CancellationToken cancellationToken)
    {
        var list = (AttributeListSyntax)attribute.Parent!;
        if (list.Attributes.Count > 1)
        {
            // The separator goes with the attribute: [ValueObject<Guid>, ProtoContract] becomes [ValueObject<Guid>].
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            editor.RemoveNode(attribute);

            return editor.GetChangedDocument();
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var previous = list.GetFirstToken().GetPreviousToken();
        var next = list.GetLastToken().GetNextToken();
        (SyntaxToken Token, SyntaxToken Rewritten) handed;
        if (!previous.IsKind(SyntaxKind.None)
            && !previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia)
            && list.GetTrailingTrivia().Any(SyntaxKind.EndOfLineTrivia))
        {
            // A list sharing its line with what precedes it, [ValueObject<string>] [ProtoContract], and ending that
            // line: the line break stays with what precedes it, and the declaration below keeps its indentation.
            handed = (previous, previous.WithTrailingTrivia(SyntaxFactory.TriviaList(previous.TrailingTrivia
                .Reverse()
                .SkipWhile(static trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                .Reverse()
                .Append(list.GetTrailingTrivia().First(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia))))));
        }
        else
        {
            // Removing the list with its trivia would drop the doc comment and the directives above it, a #pragma whose
            // restore stays below among them. They are handed to the token that follows, in place of its indentation,
            // and the list goes with what trails it, on its own line.
            var kept = list.GetLeadingTrivia().AddRange(next.LeadingTrivia.SkipWhile(static trivia =>
                trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia)));
            handed = (next, next.WithLeadingTrivia(kept));
        }

        var removed = new SyntaxAnnotation();
        var marked = root!.ReplaceSyntax(
            [list],
            (_, rewritten) => rewritten.WithAdditionalAnnotations(removed),
            [handed.Token],
            (_, _) => handed.Rewritten,
            trivia: null,
            computeReplacementTrivia: null);

        return document.WithSyntaxRoot(marked.RemoveNode(marked.GetAnnotatedNodes(removed).Single(), SyntaxRemoveOptions.KeepNoTrivia)!);
    }
}
