using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;

namespace AdCodicem.ValueObjects.Identifiers.MongoDB;

/// <summary>
/// Registers the id generators of entity identifiers with MongoDB.Driver, so that a document inserted without its
/// identifier is given a new one.
/// </summary>
/// <remarks>
/// <para>
/// Call it at start-up, beside <c>ValueObjectBson.Register</c>, which the identifier's serializer still comes from,
/// and before any class map of a document holding an identifier is built: the driver gives a class map's id member the
/// generator registered for its type when it builds the class map, and never looks again.
/// </para>
/// <code>
/// BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
/// ValueObjectBson.Register(typeof(AccountId).Assembly);
/// EntityIdBson.Register(typeof(AccountId).Assembly);
///
/// await accounts.InsertOneAsync(new Account { Name = "Ada" }); // account.Id is now acc_…
/// </code>
/// </remarks>
public static class EntityIdBson
{
    /// <summary>
    /// Registers the generator of every entity identifier registered, those the assemblies declare included, unless
    /// the driver holds one for it already.
    /// </summary>
    /// <param name="assemblies">
    /// Assemblies declaring entity identifiers, whose generated registration runs first, so that their identifiers are
    /// registered.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Each generator is closed over its identifier at compile time, through the type the descriptor hands back to a
    /// visitor (<see cref="EntityIdDescriptor.Accept{TResult}(IEntityIdVisitor{TResult})"/>). A generator of the
    /// application's own, registered first, is kept; the checker the driver gives a type that has none, under
    /// <see cref="BsonSerializer.UseZeroIdChecker"/> or <see cref="BsonSerializer.UseNullIdChecker"/>, is not one.
    /// Locating the generated registration of an assembly given by name reads its metadata, which trimming may remove.
    /// </remarks>
    [RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
    public static void Register(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
        }

        foreach (var descriptor in EntityIdRegistry.GetRegistered())
        {
            descriptor.Accept(Registration.Instance);
        }
    }

    /// <summary>
    /// Registers the generator of one entity identifier, unless the driver holds one for it already.
    /// </summary>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <remarks>
    /// A second call does nothing, and a generator of the application's own, registered first, is kept. The checker the
    /// driver gives a type that has no generator, under <see cref="BsonSerializer.UseZeroIdChecker"/> or
    /// <see cref="BsonSerializer.UseNullIdChecker"/>, is not one of the application's: it would refuse, or let through,
    /// the default instance rather than mint an identifier, so it is replaced. Closed at compile time, the registration
    /// needs no reflection.
    /// </remarks>
    public static void Register<TId>()
        where TId : struct, IEntityId<TId>
    {
        if (IsUnset<TId>(BsonSerializer.LookupIdGenerator(typeof(TId))))
        {
            BsonSerializer.RegisterIdGenerator(typeof(TId), new EntityIdGenerator<TId>());
        }
    }

    /// <summary>
    /// Tells no generator from one of the application's own: the lookup creates, for a type that has none, the checker
    /// the driver's options ask for, a <see cref="ZeroIdChecker{T}"/> or <see cref="NullIdChecker.Instance"/>.
    /// </summary>
    private static bool IsUnset<TId>(IIdGenerator? generator)
        where TId : struct, IEntityId<TId>
        => generator is null || generator.GetType() == typeof(ZeroIdChecker<TId>) || ReferenceEquals(generator, NullIdChecker.Instance);

    /// <summary>
    /// Registers the generator of the identifier a descriptor stands for.
    /// </summary>
    private sealed class Registration : IEntityIdVisitor<bool>
    {
        public static readonly Registration Instance = new();

        public bool Visit<TId>()
            where TId : struct, IEntityId<TId>
        {
            Register<TId>();
            return true;
        }
    }
}
