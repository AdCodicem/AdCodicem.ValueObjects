using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

/// <summary>
/// The typed way out of an entity identifier's descriptor: the visitor receives the identifier type it was built for,
/// and reaches the members only an identifier has through the constraint.
/// </summary>
public sealed class EntityIdDescriptorTests
{
    [Fact]
    public void Every_registered_identifier_is_handed_to_a_visitor_as_its_own_type()
    {
        foreach (var descriptor in EntityIdRegistry.GetRegistered())
        {
            descriptor.Accept(new TypeOf()).Should().Be(descriptor.ValueObjectType);
        }
    }

    [Fact]
    public void A_visitor_reaches_what_only_an_identifier_has()
    {
        EntityIdDescriptor.For<AccountId>().Accept(new Minter()).Should().StartWith("acc_");
        EntityIdDescriptor.For<HandWrittenId<RegisteredProfile>>().Accept(new TypeOf()).Should().Be<HandWrittenId<RegisteredProfile>>();
    }

    [Fact]
    public void The_visitor_is_required()
        => FluentActions.Invoking(() => EntityIdDescriptor.For<AccountId>().Accept<Type>(null!)).Should().Throw<ArgumentNullException>();

    private sealed class TypeOf : IEntityIdVisitor<Type>
    {
        public Type Visit<TId>()
            where TId : struct, IEntityId<TId>
            => typeof(TId);
    }

    private sealed class Minter : IEntityIdVisitor<string>
    {
        public string Visit<TId>()
            where TId : struct, IEntityId<TId>
            => TId.New().Value;
    }
}
