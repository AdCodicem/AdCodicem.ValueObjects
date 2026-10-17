using AdCodicem.ValueObjects.Orleans;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The Orleans package's serializer as packed, on the next major: a value object written as the underlying value it
/// replaces, with its type where it is held as an object, read back through its rules with the code of a refusal, a
/// default its type refuses never written, and a <see cref="ValueObjectException"/> carried with its code. No grain here:
/// the package brings Orleans' serializer alone, as every silo and client takes it.
/// </summary>
public sealed class OrleansTests
{
    private static readonly Serializer Strict = Build(static serializer => serializer.AddValueObjects());

    private static readonly Serializer Bare = Build(static _ => { });

    [Fact]
    public void A_value_object_is_written_as_the_value_it_carries()
    {
        Strict.SerializeToArray(Quantity.Create(3)).Should().Equal(Bare.SerializeToArray(3));
        Strict.SerializeToArray(new List<Quantity> { Quantity.Create(3), Quantity.Create(4) })
            .Should().Equal(Bare.SerializeToArray(new List<int> { 3, 4 }));
        Strict.Deserialize<List<Quantity>>(Bare.SerializeToArray(new List<int> { 3, 4 })).Should().Equal(Quantity.Create(3), Quantity.Create(4));
        Strict.Deserialize<Iban>(Bare.SerializeToArray("fr76 3000 6000 0112 3456 7890 189")).Value.Should().Be("FR7630006000011234567890189");
    }

    [Fact]
    public void A_value_object_held_as_an_object_is_written_with_its_type()
    {
        object[] values = [Quantity.Create(5), Reference<PurchaseOrder>.Create("po-1"), new List<Quantity> { Quantity.Create(1) }];

        foreach (var value in values)
        {
            Strict.Deserialize<object>(Strict.SerializeToArray(value)).Should().BeEquivalentTo(value);
        }
    }

    [Fact]
    public void A_value_its_rules_refuse_is_refused_with_its_code()
    {
        var refusal = FluentActions.Invoking(() => Strict.Deserialize<Quantity>(Bare.SerializeToArray(500)))
            .Should().Throw<SerializerException>().Which;

        refusal.Message.Should().Be("The value read is not a valid Quantity: The value must be less than or equal to 100.");
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_default_its_type_refuses_is_never_written()
    {
#pragma warning disable VO0010 // the default instance, which its rules refuse, on purpose
        FluentActions.Invoking(() => Strict.SerializeToArray(default(Quantity))).Should().Throw<SerializerException>()
            .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
#pragma warning restore VO0010
    }

    [Fact]
    public void A_value_object_exception_is_carried_with_its_code_and_without_its_value()
    {
        ValueObjectException thrown;
        try
        {
            _ = Quantity.Create(500);
            throw new InvalidOperationException("Nothing was refused.");
        }
        catch (ValueObjectException exception)
        {
            thrown = exception;
        }

        var read = Strict.Deserialize<Exception>(Strict.SerializeToArray<Exception>(thrown)).Should().BeOfType<ValueObjectException>().Which;

        read.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        read.ValueObjectType.Should().Be<Quantity>();
        read.AttemptedValue.Should().BeNull();
    }

    private static Serializer Build(Action<ISerializerBuilder> configure)
        => new ServiceCollection().AddSerializer(configure).BuildServiceProvider().GetRequiredService<Serializer>();
}
