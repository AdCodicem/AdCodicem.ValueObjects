using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a value object that code another source generator wrote brings into existence uninitialized.
/// </summary>
/// <remarks>
/// <para>
/// Source generators never see each other's output, so a generator that builds objects - a mapper, a
/// configuration binder - finds a value object without the members this generator adds to it, and writes
/// <c>new T()</c>. The build succeeds, since <c>VO0010</c> leaves generated code alone, and every value goes through
/// as a default instance nothing validated.
/// </para>
/// <para>
/// Analyzing all generated code is not an option: this generator's own output assigns <c>default</c> on its
/// rejection paths, and the System.Text.Json generator legitimately writes <c>() =&gt; new T()</c> into the metadata
/// of a source-generated context. So the two operations <c>VO0010</c> reports, an explicit <c>default</c> and a
/// parameterless <c>new</c>, are reported here only in code a listed tool generated: the member holding them, or
/// a type containing it, carries <c>[GeneratedCode(tool, ...)]</c> with that tool in the list. Riok.Mapperly marks
/// each method it writes, the configuration binding generator the class it writes, so the lookup walks from the
/// member outwards, and the first <c>[GeneratedCode]</c> it meets decides: a member another tool marked inside a
/// type a listed one marked is that other tool's.
/// </para>
/// <para>
/// The list holds <c>Riok.Mapperly</c> and <c>Microsoft.Extensions.Configuration.Binder.SourceGeneration</c>, and
/// <c>adcodicem_value_objects.generated_code_tools</c> adds names to it, comma-separated, each compared ordinally
/// with the first argument of the attribute, in full. The key is read from the global options, which a
/// <c>.globalconfig</c> sets for every file of the project: a section of an <c>.editorconfig</c> reaches a
/// generated file only when the intermediate output directory, where its path lies, is beneath that
/// <c>.editorconfig</c>, which the artifacts layout, for one, is not.
/// </para>
/// <para>
/// It is reported in the generated file, which the consumer cannot edit, so the message names the generator and
/// points to the fix on the consumer's side. A value object declared with <c>AllowDefault = true</c> is left alone,
/// as for <c>VO0010</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedUninitializedValueObjectAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The analyzer configuration key whose comma-separated tool names add to the default list.</summary>
    private const string ToolsKey = "adcodicem_value_objects.generated_code_tools";

    /// <summary>
    /// The tools whose output is checked without configuration, as each names itself in <c>[GeneratedCode]</c>.
    /// </summary>
    private static readonly string[] DefaultTools =
    [
        "Riok.Mapperly",
        "Microsoft.Extensions.Configuration.Binder.SourceGeneration",
    ];

    /// <summary>
    /// Reports a value object created uninitialized by code a listed generator wrote.
    /// </summary>
    public static readonly DiagnosticDescriptor CreatedByGeneratedCode = new(
        "VO0032",
        "Value object created uninitialized by generated code",
        "'{0}' is created uninitialized by code that '{1}' generated. Map it through a method that calls Create, or "
        + "keep the underlying type in what that generator binds.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Another source generator cannot see the members this generator adds, so it may write new T() "
        + "or default for a value object, which skips every rule the type declares. This is reported in the code "
        + "that Riok.Mapperly and the configuration binding generator write, and in the code of any tool that "
        + "adcodicem_value_objects.generated_code_tools names in a .globalconfig.",
        helpLinkUri: "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/reference/diagnostics"
        + "#a-value-object-another-generator-creates");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [CreatedByGeneratedCode];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        // Generated code is the whole point: the code other generators write, which VO0010 does not look at.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var annotations = ValueObjectAnnotations.Resolve(compilationContext.Compilation);
            if (annotations.IsEmpty)
            {
                return;
            }

            var tools = Tools(compilationContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeDefault(operationContext, annotations, tools),
                OperationKind.DefaultValue);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeCreation(operationContext, annotations, tools),
                OperationKind.ObjectCreation);
        });
    }

    /// <summary>
    /// Reads the tools to check: the defaults, and the names the configuration adds to them.
    /// </summary>
    private static ImmutableHashSet<string> Tools(AnalyzerConfigOptions options)
    {
        var tools = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        tools.UnionWith(DefaultTools);

        if (options.TryGetValue(ToolsKey, out var configured))
        {
            foreach (var entry in configured.Split(','))
            {
                var tool = entry.Trim();
                if (tool.Length > 0)
                {
                    tools.Add(tool);
                }
            }
        }

        return tools.ToImmutable();
    }

    private static void AnalyzeDefault(
        OperationAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> annotations,
        ImmutableHashSet<string> tools)
    {
        if (!context.Operation.IsImplicit)
        {
            Report(context, context.Operation.Type, annotations, tools);
        }
    }

    private static void AnalyzeCreation(
        OperationAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> annotations,
        ImmutableHashSet<string> tools)
    {
        if (context.Operation is IObjectCreationOperation { Arguments.Length: 0, IsImplicit: false } creation)
        {
            Report(context, creation.Type, annotations, tools);
        }
    }

    private static void Report(
        OperationAnalysisContext context,
        ITypeSymbol? type,
        ImmutableArray<INamedTypeSymbol> annotations,
        ImmutableHashSet<string> tools)
    {
        // The type test is the cheap one, and the one that almost always fails: the attributes are read only for a
        // value object.
        if (ValueObjectAnnotations.RefusingDefault(type, annotations) is not { } valueObject
            || GeneratingTool(context.ContainingSymbol) is not { } tool
            || !tools.Contains(tool))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            CreatedByGeneratedCode,
            context.Operation.Syntax.GetLocation(),
            valueObject.Name,
            tool));
    }

    /// <summary>
    /// Gets the tool that generated a member: the first argument of the first <c>[GeneratedCode]</c> met on the way
    /// from the member out through the types containing it, the property or event of an accessor included.
    /// </summary>
    /// <returns>The tool's name, or <see langword="null"/> when nothing on the way says a tool generated it.</returns>
    private static string? GeneratingTool(ISymbol member)
    {
        for (var symbol = member; symbol is not null and not INamespaceSymbol; symbol = symbol.ContainingSymbol)
        {
            var attribute = GeneratedCode(symbol)
                ?? (symbol is IMethodSymbol { AssociatedSymbol: { } associated } ? GeneratedCode(associated) : null);

            if (attribute is not null)
            {
                // An attribute the compiler could not bind has no argument, and names no tool.
                var arguments = attribute.ConstructorArguments;
                return arguments.Length > 0 ? arguments[0].Value as string : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the <c>[GeneratedCode]</c> a symbol carries, matched by name: a copy of the attribute in another assembly
    /// says the same thing.
    /// </summary>
    private static AttributeData? GeneratedCode(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "GeneratedCodeAttribute" } attributeClass
                && attributeClass.ToDisplayString() == "System.CodeDom.Compiler.GeneratedCodeAttribute")
            {
                return attribute;
            }
        }

        return null;
    }
}
