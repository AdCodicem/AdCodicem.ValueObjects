using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.GeneratorTests.Internals;

/// <summary>
/// The model the emitters read.
/// </summary>
/// <remarks>
/// The generator builds a model only after resolving its underlying type, and an unsupported one stops at
/// <c>VO0003</c>, so a model naming a type outside the table is something only a direct call produces.
/// </remarks>
public sealed class ValueObjectModelTests
{
    [Fact]
    public void A_model_naming_a_type_outside_the_underlying_table_refuses_to_resolve_it()
    {
        var model = new ValueObjectModel
        {
            Namespace = string.Empty,
            TypeName = "Address",
            Identifier = "Address",
            QualifiedName = "global::Address",
            ContainingTypes = EquatableArray<string>.Empty,
            Kind = UnderlyingKind.String,
            UnderlyingFullName = "global::System.Uri",
            HintName = "Address.g.cs",
        };

        var read = () => model.Underlying;

        read.Should().Throw<InvalidOperationException>().WithMessage("*'global::System.Uri'*");
    }
}
