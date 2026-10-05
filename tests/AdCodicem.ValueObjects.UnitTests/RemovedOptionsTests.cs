using System.Reflection;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The options the generator once read as text, which nothing reads any more: each stays, until a minor version removes
/// it, as a compile error that names its replacement, rather than a member that compiles and does nothing.
/// </summary>
public sealed class RemovedOptionsTests
{
    // Found by name, as a reader of the attributes finds them.
    public static TheoryData<MemberInfo, string, string> RemovedOptions => new()
    {
        { typeof(ValueObjectAttribute<string>).GetProperty("Pattern")!, "VO0021", "IValueObjectPatternValidator" },
        { typeof(ValueObjectAttribute<int>).GetProperty("Minimum")!, "VO0028", "IValueObjectMinimum<T>" },
        { typeof(ValueObjectAttribute<int>).GetProperty("Maximum")!, "VO0028", "IValueObjectMaximum<T>" },
        { KnownValueConstructor(), "VO0034", "Known(value)" },
        { typeof(ValueObjectAttribute<string>).GetProperty("Example")!, "VO0035", "IValueObjectExample<TSelf>" },
        { typeof(EntityIdAttribute).GetProperty("Example")!, "VO0035", "IValueObjectExample<TSelf>" },
    };

    [Theory]
    [MemberData(nameof(RemovedOptions))]
    public void A_removed_option_is_a_compile_error_that_names_its_replacement(MemberInfo option, string diagnostic, string replacement)
    {
        var obsolete = option.GetCustomAttribute<ObsoleteAttribute>();

        obsolete.Should().NotBeNull();
        obsolete!.IsError.Should().BeTrue();
        obsolete.DiagnosticId.Should().Be(diagnostic);
        obsolete.Message.Should().Contain(replacement);
    }

    /// <summary>
    /// The constructor that took a name and a value cannot be called from code that compiles, but a reader of the
    /// attributes of a type still built through reflection creates one: it keeps nothing, and declares no known value.
    /// </summary>
    [Fact]
    public void The_constructor_that_took_a_name_and_a_value_keeps_nothing()
    {
        var attribute = (KnownValueAttribute)KnownValueConstructor().Invoke(["France", "FR"]);

        attribute.Description.Should().BeNull();
    }

    private static ConstructorInfo KnownValueConstructor()
        => typeof(KnownValueAttribute).GetConstructor([typeof(string), typeof(object)])!;
}
