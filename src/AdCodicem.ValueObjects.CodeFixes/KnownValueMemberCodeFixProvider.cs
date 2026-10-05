using System.Collections.Immutable;
using System.Composition;
using AdCodicem.ValueObjects.Generators;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace AdCodicem.ValueObjects.CodeFixes;

/// <summary>
/// Rewrites the known values a value object declares on its type, <c>[KnownValue("France", "FR")]</c>, as the members
/// that declare them now, <c>[KnownValue] public static readonly CountryCode France = Known("FR");</c> (<c>VO0034</c>).
/// </summary>
/// <remarks>
/// <para>
/// The compiler reports each attribute, through the <c>[Obsolete]</c> on the constructor taking a name and a value. One
/// fix rewrites every one of them on the declaration, in the order they were written, which is the order
/// <c>KnownValues</c> and the OpenAPI <c>enum</c> list them in, at the top of the type: fixing them one at a time would
/// reverse it. Fixing all of a document does the same for each declaration.
/// </para>
/// <para>
/// The value is written as an expression of the underlying type, a value written as text included: <c>"1970-01-01"</c>
/// on a <c>DateOnly</c> becomes <c>new DateOnly(1970, 1, 1)</c>. A <c>Description</c> stays on the attribute. An
/// attribute the generator refused, for a name that is not an identifier or a value that does not read, stays as it is.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(KnownValueMemberCodeFixProvider))]
[Shared]
public sealed class KnownValueMemberCodeFixProvider : CodeFixProvider
{
    /// <summary>The identifier of the <c>[Obsolete]</c> on the constructor of <c>[KnownValue]</c> that takes a name and a value.</summary>
    public const string DiagnosticId = "VO0034";

    private const string KnownValueAttributeName = "AdCodicem.ValueObjects.Annotations.KnownValueAttribute";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [DiagnosticId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => FixAllProvider.Create(static async (context, document, diagnostics) =>
        {
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var declarations = diagnostics
                .Select(diagnostic => Declaration(root, diagnostic))
                .OfType<TypeDeclarationSyntax>()
                .Distinct()
                .ToList();

            return await RewriteAsync(document, declarations, context.CancellationToken).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            // Offered only where the attribute reported can be rewritten: one the generator refused stays as it is.
            if (Declaration(root, diagnostic) is not { } declaration
                || Attribute(root, diagnostic) is not { } attribute
                || model.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type
                || Underlying(type) is not { } underlying
                || Member(attribute, model, declaration, underlying, context.CancellationToken) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Declare the known values of '{declaration.Identifier.ValueText}' as members",
                    cancellationToken => RewriteAsync(context.Document, [declaration], cancellationToken),
                    equivalenceKey: nameof(KnownValueMemberCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>
    /// Finds the attribute a diagnostic reports.
    /// </summary>
    private static AttributeSyntax? Attribute(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<AttributeSyntax>();

    /// <summary>
    /// Finds the declaration of the type that carries the attribute a diagnostic reports.
    /// </summary>
    private static TypeDeclarationSyntax? Declaration(SyntaxNode root, Diagnostic diagnostic)
        => Attribute(root, diagnostic)?.Parent?.Parent as TypeDeclarationSyntax;

    private static async Task<Document> RewriteAsync(
        Document document,
        IReadOnlyList<TypeDeclarationSyntax> declarations,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var model = editor.SemanticModel;

        foreach (var declaration in declarations)
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { } type
                || Underlying(type) is not { } underlying)
            {
                continue;
            }

            var members = new List<MemberDeclarationSyntax>();
            foreach (var list in declaration.AttributeLists)
            {
                var rewritten = new List<AttributeSyntax>();
                foreach (var attribute in list.Attributes)
                {
                    if (Member(attribute, model, declaration, underlying, cancellationToken) is { } member)
                    {
                        members.Add(member);
                        rewritten.Add(attribute);
                    }
                }

                if (rewritten.Count == 0)
                {
                    continue;
                }

                if (rewritten.Count == list.Attributes.Count)
                {
                    editor.RemoveNode(list);
                }
                else
                {
                    foreach (var attribute in rewritten)
                    {
                        editor.RemoveNode(attribute);
                    }
                }
            }

            if (members.Count > 0)
            {
                // Replaced last, so that the attributes removed above are already gone from the node it is handed.
                editor.ReplaceNode(declaration, (current, _) => WithMembers((TypeDeclarationSyntax)current, members));
            }
        }

        return editor.GetChangedDocument();
    }

    /// <summary>
    /// Puts the members a fix writes at the top of a type, each separated from the next by a blank line, and gives the
    /// type a body when it was declared without one.
    /// </summary>
    /// <remarks>
    /// The line breaks are the formatter's to write: it lays out each member it is handed with the line ending the
    /// document's options give, whatever ending the break was created with.
    /// </remarks>
    private static TypeDeclarationSyntax WithMembers(TypeDeclarationSyntax type, List<MemberDeclarationSyntax> members)
    {
        var separated = new List<MemberDeclarationSyntax>();
        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            if (index < members.Count - 1 || type.Members.Count > 0)
            {
                member = member.WithTrailingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed);
            }

            separated.Add(member);
        }

        if (!type.SemicolonToken.IsKind(SyntaxKind.None))
        {
            // A declaration ending in a semicolon has no body: one replaces the semicolon, which the formatter then lays out.
            var trailing = type.SemicolonToken.TrailingTrivia;
            type = type
                .WithSemicolonToken(default)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTrailingTrivia(trailing))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        return type.WithMembers(type.Members.InsertRange(0, separated));
    }

    /// <summary>
    /// Writes the member that declares the known value an attribute on the type declares, if the attribute is one and
    /// the generator could read it.
    /// </summary>
    private static MemberDeclarationSyntax? Member(
        AttributeSyntax attribute,
        SemanticModel model,
        TypeDeclarationSyntax declaration,
        UnderlyingType underlying,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(attribute, cancellationToken).Symbol is not IMethodSymbol { Parameters.Length: 2 } constructor
            || constructor.ContainingType.ToDisplayString() != KnownValueAttributeName
            || attribute.ArgumentList?.Arguments is not { Count: >= 2 } arguments
            || model.GetConstantValue(arguments[0].Expression, cancellationToken) is not { HasValue: true, Value: string name }
            || !SyntaxFacts.IsValidIdentifier(name)
            || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            return null;
        }

        var constant = model.GetConstantValue(arguments[1].Expression, cancellationToken);
        if (!constant.HasValue || KnownValueExpression.Write(underlying, constant.Value) is not { } value)
        {
            return null;
        }

        // The named arguments other than the name and the value, Description among them, are kept as written.
        var named = arguments.Where(static argument => argument.NameEquals is not null).Select(static argument => argument.ToString()).ToList();
        var annotation = named.Count == 0 ? attribute.Name.ToString() : $"{attribute.Name}({string.Join(", ", named)})";
        var type = declaration.Identifier.Text + declaration.TypeParameterList;

        return SyntaxFactory.ParseMemberDeclaration($"[{annotation}]\npublic static readonly {type} {name} = Known({value});\n")!
            .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);
    }

    /// <summary>
    /// Reads the underlying type of a value object off its <c>[ValueObject&lt;T&gt;]</c>.
    /// </summary>
    private static UnderlyingType? Underlying(INamedTypeSymbol type)
    {
        var attribute = ValueObjectGenerator.FindAttribute(type, ValueObjectGenerator.ValueObjectAttributeName);
        var argument = attribute?.AttributeClass?.TypeArguments.FirstOrDefault();

        return argument is not null && UnderlyingType.TryResolve(argument.ToDisplayString(ValueObjectGenerator.QualifiedFormat), out var underlying)
            ? underlying
            : null;
    }
}
