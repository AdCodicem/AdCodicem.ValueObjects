using System.IO;
using System.Reflection;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Identifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// Asserts that the agent skill still covers the whole surface a consumer writes against.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DocumentationSnippetTests"/> proves that what the skill says compiles. These tests prove the other
/// direction, which is the one that rots silently: an option, a hook, a diagnostic or an error code added to the
/// library and never written down. A skill that has gone quiet about half the surface still passes every
/// snippet, and an agent reading it simply never learns the new thing exists.
/// </para>
/// <para>
/// Each of these reads the shipped assemblies by reflection, so extending the library is what fails the build —
/// not a reviewer remembering to look. When one fails, add the missing entry to the skill file it names.
/// </para>
/// </remarks>
public sealed class SkillCoverageTests
{
    [Fact]
    public void Every_option_of_the_value_object_attribute_is_documented()
    {
        var skill = Skill();

        foreach (var option in Options(typeof(ValueObjectAttribute<>)))
        {
            skill.Should().Contain(
                option,
                "'{0}' is an option of [ValueObject<T>] and belongs in references/authoring.md",
                option);
        }
    }

    [Fact]
    public void Every_option_of_the_entity_id_attribute_is_documented()
    {
        var skill = Skill();

        foreach (var option in Options(typeof(EntityIdAttribute)))
        {
            skill.Should().Contain(
                option,
                "'{0}' is an option of [EntityId] and belongs in references/identifiers.md",
                option);
        }
    }

    [Fact]
    public void Every_hook_interface_and_the_member_it_declares_is_documented()
    {
        var skill = Skill();

        var hooks = typeof(IValueObject).Assembly.GetExportedTypes()
            .Where(type => type.IsInterface && IsHook(type))
            .ToList();

        hooks.Should().NotBeEmpty("the reflection filter must keep matching the hook interfaces");

        foreach (var hook in hooks)
        {
            var name = hook.Name.Split('`')[0];

            skill.Should().Contain(name, "'{0}' is a hook a consumer implements", name);

            foreach (var member in StaticAbstractMembers(hook))
            {
                skill.Should().Contain(
                    member,
                    "'{0}' is the member '{1}' requires, and a consumer has to write it by hand",
                    member,
                    name);
            }
        }
    }

    [Fact]
    public void Every_member_of_the_typed_contract_is_documented()
    {
        var skill = Skill();

        foreach (var member in StaticAbstractMembers(typeof(IValueObject<,>)))
        {
            skill.Should().Contain(
                member,
                "'{0}' is generated on every value object, so the skill must say it exists rather than let an "
                + "agent write it again",
                member);
        }
    }

    [Fact]
    public void Every_diagnostic_is_documented()
    {
        var diagnostics = Diagnostics();
        var documented = File.ReadAllText(Path.Combine(SkillDirectory(), "references", "diagnostics.md"));

        diagnostics.Should().HaveCountGreaterThan(10, "the reflection must keep finding the descriptors");

        foreach (var id in diagnostics)
        {
            documented.Should().Contain(id, "'{0}' must be listed in references/diagnostics.md with its fix", id);
        }
    }

    [Fact]
    public void Every_well_known_error_code_is_documented()
    {
        var skill = Skill();

        var codes = typeof(ValueObjectErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        foreach (var code in codes)
        {
            skill.Should().Contain(code, "'{0}' is a code a consumer branches on", code);
        }
    }

    /// <summary>
    /// A hook is any interface a consumer implements to declare a rule. The naming is the contract: a new one
    /// called <c>IValueObjectSanitizer</c> would be picked up here and have to be documented.
    /// </summary>
    private static bool IsHook(Type type)
        => type.Namespace == "AdCodicem.ValueObjects"
           && type.Name.StartsWith("IValueObject", StringComparison.Ordinal)
           && (type.Name.EndsWith("Normalizer", StringComparison.Ordinal)
               || type.Name.EndsWith("Validator", StringComparison.Ordinal)
               || type.Name.EndsWith("Formatter", StringComparison.Ordinal));

    private static IEnumerable<string> Options(Type attribute)
        => attribute
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

    private static IEnumerable<string> StaticAbstractMembers(Type contract)
        => contract
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.IsAbstract)
            .Select(method => method.Name)
            .Concat(contract.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>
    /// Reads the diagnostic identifiers out of the generator: the descriptors it reports while generating, plus
    /// the ones its analyzers support. <c>DiagnosticDescriptors</c> is internal to the generator, so this goes
    /// through reflection rather than adding an <c>InternalsVisibleTo</c> for the sake of a test.
    /// </summary>
    private static IReadOnlyList<string> Diagnostics()
    {
        var generator = typeof(Generators.ValueObjectGenerator).Assembly;

        var descriptors = generator
            .GetType("AdCodicem.ValueObjects.Generators.Diagnostics.DiagnosticDescriptors", throwOnError: true)!
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => ((DiagnosticDescriptor)field.GetValue(null)!).Id);

        var analyzers = new DiagnosticAnalyzer[] { new ValueObjectHookAnalyzer(), new UninitializedValueObjectAnalyzer() }
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .Select(descriptor => descriptor.Id);

        return [.. descriptors.Concat(analyzers).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)];
    }

    /// <summary>Every skill file, concatenated: a surface may be documented in any of them.</summary>
    private static string Skill()
        => string.Join(
            '\n',
            Directory
                .EnumerateFiles(SkillDirectory(), "*.md", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

    private static string SkillDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");

        return Path.Combine(directory!.FullName, "skills", "value-objects");
    }
}
