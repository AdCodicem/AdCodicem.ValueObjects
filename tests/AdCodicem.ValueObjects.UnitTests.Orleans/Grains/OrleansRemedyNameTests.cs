using System.Reflection;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Orleans;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// The names <c>VO0043</c>'s remedy of <c>[GenerateSerializer]</c> writes for this family's package, checked against
/// the package: the analyzer names it by string, and the core and the satellites ride two release trains (ADR 0012),
/// so a rename in one fails the other's tests rather than a consumer's build. Read off the analyzer's source, which
/// the project embeds: it cannot load the analyzer, Microsoft.CodeAnalysis.CSharp 4.14.0 refusing the
/// Microsoft.CodeAnalysis.Common 5.0.0 Orleans brings (NU1608).
/// </summary>
public sealed class OrleansRemedyNameTests
{
    [Fact]
    public void The_registration_the_analyzer_names_is_the_packages_own()
    {
        var source = AnalyzerSource();
        var extensions = typeof(ValueObjectSerializerBuilderExtensions);
        var package = extensions.Assembly.GetName().Name;
        const string registration = nameof(ValueObjectSerializerBuilderExtensions.AddValueObjects);

        var reached = Regex.Match(source, "OrleansExtensionsName = \"([^\"]+)\";");
        reached.Success.Should().BeTrue("the analyzer finds the package by the name of its registration's class");
        reached.Groups[1].Value.Should().Be(extensions.FullName);
        source.Should().Contain($"\"Remove the attribute, reference {package} and call {registration}() on the \"")
            .And.Contain($"\"Remove the attribute: {registration}() of {package} serializes the value object \"");
    }

    private static string AnalyzerSource()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SerializerContractAnalyzer.cs")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
