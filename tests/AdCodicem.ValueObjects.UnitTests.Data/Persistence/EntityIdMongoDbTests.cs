using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Identifiers.MongoDB;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.Identifiers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The id generator MongoDB.Driver asks for a document inserted without its entity identifier: when it mints one, what
/// it mints, and how it is registered. The integration suite inserts such documents into a server.
/// </summary>
public sealed class EntityIdMongoDbTests
{
    public EntityIdMongoDbTests() => MongoDb.EnsureRegistered();

#pragma warning disable VO0010 // The uninitialized identifier is what the driver finds on a document inserted without one.
    [Fact]
    public void An_identifier_never_assigned_is_empty_and_any_other_is_not()
    {
        var generator = new EntityIdGenerator<AccountId>();

        generator.IsEmpty(default(AccountId)).Should().BeTrue();
        generator.IsEmpty(null).Should().BeTrue();
        generator.IsEmpty(AccountId.New().Value).Should().BeTrue("text is not an identifier of the type");
        generator.IsEmpty(SubscriptionId.New()).Should().BeTrue("an identifier of another type is not one of this type");
        generator.IsEmpty(AccountId.New()).Should().BeFalse();
    }
#pragma warning restore VO0010

    [Fact]
    public void A_new_identifier_is_minted_under_the_clock_and_the_entropy_of_the_flow()
    {
        var clock = new StoppedClock(new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero));

        object minted;
        using (ValueObjectIds.Use(clock, new DeterministicEntropy(7)))
        {
            minted = new EntityIdGenerator<AccountId>().GenerateId(container: new object(), document: new object());
        }

        minted.Should().Be(AccountId.New(clock, new DeterministicEntropy(7)));
    }

    /// <summary>
    /// The registration gives every identifier the suite declares its generator, which a class map built afterwards
    /// takes for its id member.
    /// </summary>
    [Fact]
    public void A_class_map_built_after_the_registration_mints_the_identifier_of_its_id_member()
    {
        BsonSerializer.LookupIdGenerator(typeof(SubscriptionId)).Should().BeOfType<EntityIdGenerator<SubscriptionId>>();
        BsonSerializer.LookupIdGenerator(typeof(LedgerEntryId)).Should().BeOfType<EntityIdGenerator<LedgerEntryId>>();

        var id = BsonClassMap.LookupClassMap(typeof(MongoAccount)).IdMemberMap;

        id.ElementName.Should().Be("_id");
        id.IdGenerator.Should().BeOfType<EntityIdGenerator<AccountId>>();

        var account = new MongoAccount { Name = "Ada" };
        id.IdGenerator.IsEmpty(id.Getter(account)).Should().BeTrue();
        id.Setter(account, id.IdGenerator.GenerateId(new object(), account));
        account.Id.Value.Should().StartWith("acc_");
        account.ToBsonDocument()["_id"].Should().Be(new BsonString(account.Id.Value));
    }

    [Fact]
    public void A_generator_already_registered_for_an_identifier_is_kept()
    {
        var own = new OwnGenerator();
        BsonSerializer.RegisterIdGenerator(typeof(HandWrittenId<OwnGeneratorProfile>), own);

        EntityIdBson.Register<HandWrittenId<OwnGeneratorProfile>>();

        BsonSerializer.LookupIdGenerator(typeof(HandWrittenId<OwnGeneratorProfile>)).Should().BeSameAs(own);
    }

    /// <summary>
    /// The checker the driver gives a type that has no generator, under <c>BsonSerializer.UseZeroIdChecker</c> or
    /// <c>UseNullIdChecker</c>, refuses or lets through the default instance and mints nothing: it is replaced, where a
    /// checker of the application's own, derived from the driver's, is kept. <c>MongoDbStartUpTests</c> turns the
    /// options on in a driver of its own.
    /// </summary>
    [Fact]
    public void The_checker_the_driver_gives_a_type_without_a_generator_is_replaced()
    {
        BsonSerializer.RegisterIdGenerator(typeof(HandWrittenId<ZeroCheckedProfile>), new ZeroIdChecker<HandWrittenId<ZeroCheckedProfile>>());
        BsonSerializer.RegisterIdGenerator(typeof(HandWrittenId<NullCheckedProfile>), NullIdChecker.Instance);
        var own = new OwnZeroIdChecker<HandWrittenId<OwnCheckerProfile>>();
        BsonSerializer.RegisterIdGenerator(typeof(HandWrittenId<OwnCheckerProfile>), own);

        EntityIdBson.Register<HandWrittenId<ZeroCheckedProfile>>();
        EntityIdBson.Register<HandWrittenId<NullCheckedProfile>>();
        EntityIdBson.Register<HandWrittenId<OwnCheckerProfile>>();

        BsonSerializer.LookupIdGenerator(typeof(HandWrittenId<ZeroCheckedProfile>)).Should().BeOfType<EntityIdGenerator<HandWrittenId<ZeroCheckedProfile>>>();
        BsonSerializer.LookupIdGenerator(typeof(HandWrittenId<NullCheckedProfile>)).Should().BeOfType<EntityIdGenerator<HandWrittenId<NullCheckedProfile>>>();
        BsonSerializer.LookupIdGenerator(typeof(HandWrittenId<OwnCheckerProfile>)).Should().BeSameAs(own);
    }

    [Fact]
    public void A_second_registration_of_an_identifier_changes_nothing()
    {
        EntityIdBson.Register<HandWrittenId<MintedProfile>>();
        var registered = BsonSerializer.LookupIdGenerator(typeof(HandWrittenId<MintedProfile>));

        EntityIdBson.Register<HandWrittenId<MintedProfile>>();

        registered.Should().BeOfType<EntityIdGenerator<HandWrittenId<MintedProfile>>>();
        BsonSerializer.LookupIdGenerator(typeof(HandWrittenId<MintedProfile>)).Should().BeSameAs(registered);
    }

    [Fact]
    public void The_assemblies_to_register_are_required()
        => FluentActions.Invoking(() => EntityIdBson.Register(null!)).Should().Throw<ArgumentNullException>();

    /// <summary>An account as a document, whose identifier is its <c>_id</c>.</summary>
    public sealed class MongoAccount
    {
        public AccountId Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>The profile of an identifier the application mints itself.</summary>
    public sealed class OwnGeneratorProfile : IHandWrittenIdProfile
    {
        public static string Prefix => "own";
    }

    /// <summary>The profile of an identifier the driver's zero checker was given first.</summary>
    public sealed class ZeroCheckedProfile : IHandWrittenIdProfile
    {
        public static string Prefix => "zro";
    }

    /// <summary>The profile of an identifier the driver's null checker was given first.</summary>
    public sealed class NullCheckedProfile : IHandWrittenIdProfile
    {
        public static string Prefix => "nul";
    }

    /// <summary>The profile of an identifier the application checks with a checker of its own.</summary>
    public sealed class OwnCheckerProfile : IHandWrittenIdProfile
    {
        public static string Prefix => "chk";
    }

    /// <summary>The profile of an identifier the package mints.</summary>
    public sealed class MintedProfile : IHandWrittenIdProfile
    {
        public static string Prefix => "mnt";
    }

    private sealed class OwnGenerator : IIdGenerator
    {
        public object GenerateId(object container, object document) => throw new NotSupportedException();

        public bool IsEmpty(object id) => throw new NotSupportedException();
    }

    private sealed class OwnZeroIdChecker<T> : ZeroIdChecker<T>
        where T : struct, IEquatable<T>;

    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
