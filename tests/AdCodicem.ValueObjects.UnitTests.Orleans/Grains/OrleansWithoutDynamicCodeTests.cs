using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Orleans;
using AdCodicem.ValueObjects.UnitTests.TestData;
using static AdCodicem.ValueObjects.UnitTests.Grains.OrleansWire;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// The Orleans package where the runtime supports no dynamic code, as under native AOT, run under the JIT in a child
/// process of its own (<see cref="WithoutDynamicCode"/>): a value object the registry holds is serialized as anywhere
/// else, and one nothing registered is refused, naming what serializes it at compile time, which then does.
/// </summary>
/// <remarks>
/// No holder of a value object here: Orleans resolves the codec of a value type a generated holder names through an open
/// generic service, which its dependency injection refuses without dynamic code, before the package is asked. Members
/// typed <see cref="object"/> and roots reach the package.
/// </remarks>
public sealed class OrleansWithoutDynamicCodeTests
{
    private const string Reflection =
        "' is a value object nothing registered, and describing it by reflection takes dynamic code, which this runtime does "
        + "not support. Register it at start-up, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), or serialize it "
        + "with AddValueObject<TSelf, TValue>().";

    [Fact]
    public async Task A_value_object_nothing_registered_is_refused_naming_the_typed_registration()
    {
        if (await WithoutDynamicCode.RanInAChildAsync(typeof(OrleansWithoutDynamicCodeTests), nameof(A_value_object_nothing_registered_is_refused_naming_the_typed_registration)))
        {
            return;
        }

        Strict.Read<GrainBox>(Strict.SerializeToArray(new GrainBox { Value = GrainSku.Create("abc-123") })).Value
            .Should().Be(GrainSku.Create("ABC-123"));
        Strict.Deserialize<GrainSku>(Strict.SerializeToArray(GrainSku.Create("abc-123"))).Should().Be(GrainSku.Create("ABC-123"));

        FluentActions.Invoking(() => Strict.SerializeToArray(new GrainBox { Value = Reference<OrleansStaticOwner>.Create("po-1") }))
            .Should().Throw<NotSupportedException>().WithMessage($"'{typeof(Reference<OrleansStaticOwner>)}{Reflection}");
        FluentActions.Invoking(() => Strict.SerializeToArray(Declared<OrleansStaticDeclaration, string>.Create("ab")))
            .Should().Throw<NotSupportedException>().WithMessage($"'{typeof(Declared<OrleansStaticDeclaration, string>)}{Reflection}");

        var typed = Build(static builder => builder.AddValueObject<Reference<OrleansStaticOwner>, string>());
        typed.Read<GrainBox>(typed.SerializeToArray(new GrainBox { Value = Reference<OrleansStaticOwner>.Create("po-1") })).Value
            .Should().Be(Reference<OrleansStaticOwner>.Create("PO-1"));
    }
}

/// <summary>The owner of the references nothing registers where dynamic code is off, which nothing else names.</summary>
public sealed class OrleansStaticOwner;

/// <summary>A code written by hand that nothing registers where dynamic code is off, which nothing else declares.</summary>
public sealed class OrleansStaticDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };
}
