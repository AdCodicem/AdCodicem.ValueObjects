using System.Reflection;
using System.Runtime.Loader;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// <c>ValueObjectBson.Register</c> as an application calls it at start-up, in a driver nothing has configured yet: the
/// two mistakes it refuses, on every call and for every assembly it is given, and the order that passes.
/// </summary>
/// <remarks>
/// MongoDB.Driver keeps its serializers and class maps for the whole process, and a registration refused here would
/// spoil the driver every other MongoDB test of the suite uses. So each test loads its own copies of MongoDB.Bson, the
/// registry, this package and a fixture assembly of value objects, beside the suite's, and drives them by reflection:
/// a process of its own, as far as their static state goes.
/// </remarks>
public sealed class MongoDbStartUpTests
{
    private const string ConsignmentId = "AdCodicem.ValueObjects.Fixtures.XmlSerialization.ConsignmentId";

    private const string ParcelCount = "AdCodicem.ValueObjects.Fixtures.XmlSerialization.ParcelCount";

    private const string Remedy = "A value object over Guid is registered, and the serializer of Guid has no representation "
        + "(GuidRepresentation.Unspecified), so it would throw on the first write. Call "
        + "BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)) before this method.";

    [Fact]
    public void A_value_object_over_Guid_fails_the_registration_while_the_serializer_of_Guid_has_no_representation()
    {
        var driver = new FreshDriver();

        driver.Invoking(d => d.Register()).Should().Throw<InvalidOperationException>().WithMessage(Remedy);
        driver.Invoking(d => d.RegisterOne(ConsignmentId, typeof(Guid))).Should().Throw<InvalidOperationException>().WithMessage(Remedy);
    }

    [Fact]
    public void A_second_registration_repeats_the_checks_for_the_value_objects_it_brings()
    {
        var driver = new FreshDriver();

        // Nothing over Guid is registered yet: the first call passes and registers the provider.
        driver.Invoking(d => d.Register([])).Should().NotThrow();

        driver.Invoking(d => d.Register()).Should().Throw<InvalidOperationException>().WithMessage(Remedy);
    }

    [Fact]
    public void Every_assembly_given_is_registered_before_the_checks()
    {
        var driver = new FreshDriver();

        driver.Invoking(d => d.Register([typeof(object).Assembly, d.Fixture])).Should().Throw<InvalidOperationException>().WithMessage(Remedy);
    }

    [Fact]
    public void A_value_object_the_driver_mapped_before_the_registration_fails_it_by_name()
    {
        var driver = new FreshDriver();
        driver.RegisterGuidSerializer();

        // What serializing a document holding one does: the driver maps the value object through a class map, for good.
        driver.SerializerOf(ParcelCount).Name.Should().Be("BsonClassMapSerializer`1");

        driver.Invoking(d => d.Register()).Should().Throw<InvalidOperationException>()
            .WithMessage("MongoDB.Driver has already mapped ParcelCount through a class map, *");
    }

    [Fact]
    public void A_construction_of_a_generic_value_object_the_driver_mapped_before_the_registration_fails_it_by_name()
    {
        var driver = new FreshDriver();
        driver.RegisterGuidSerializer();

        // The registry holds the generic definition alone, and nothing has resolved this construction yet.
        var tag = driver.Fixture.GetType("AdCodicem.ValueObjects.Fixtures.XmlSerialization.ShipmentTag`1", throwOnError: true)!
            .MakeGenericType(driver.Fixture.GetType("AdCodicem.ValueObjects.Fixtures.XmlSerialization.Shipment", throwOnError: true)!);
        driver.SerializerOf(tag).Name.Should().Be("BsonClassMapSerializer`1");

        driver.Invoking(d => d.Register()).Should().Throw<InvalidOperationException>()
            .WithMessage("MongoDB.Driver has already mapped ShipmentTag<Shipment> through a class map, *");
    }

    [Fact]
    public void Registered_in_order_every_value_object_gets_its_serializer()
    {
        var driver = new FreshDriver();

        driver.RegisterGuidSerializer();
        driver.Register();
        driver.RegisterOne(ConsignmentId, typeof(Guid));

        driver.SerializerOf(ConsignmentId).Name.Should().Be("ValueObjectBsonSerializer`2");
        driver.SerializerOf(ParcelCount).Name.Should().Be("ValueObjectBsonSerializer`2");
    }

    /// <summary>
    /// Copies of MongoDB.Bson, the registry, the identifiers, this package and the fixture holding a value object over
    /// <see cref="Guid"/>, whose static state nothing in the suite has touched; every other assembly is the suite's.
    /// </summary>
    private sealed class FreshDriver : AssemblyLoadContext
    {
        private static readonly HashSet<string> Fresh =
        [
            "MongoDB.Bson",
            "AdCodicem.ValueObjects.Abstractions",
            "AdCodicem.ValueObjects.Identifiers",
            "AdCodicem.ValueObjects.MongoDB",
            "AdCodicem.ValueObjects.Fixtures.XmlSerialization",
        ];

        public FreshDriver()
            : base(nameof(FreshDriver))
            => Fixture = LoadFromAssemblyName(new AssemblyName("AdCodicem.ValueObjects.Fixtures.XmlSerialization"));

        /// <summary>Gets the fixture assembly, loaded in this context.</summary>
        public Assembly Fixture { get; }

        /// <summary>Registers <c>new GuidSerializer(GuidRepresentation.Standard)</c>, as an application does first.</summary>
        public void RegisterGuidSerializer()
        {
            var representation = Enum.Parse(Bson("MongoDB.Bson.GuidRepresentation"), "Standard");
            var serializer = Activator.CreateInstance(Bson("MongoDB.Bson.Serialization.Serializers.GuidSerializer"), representation);
            Call(
                Bson("MongoDB.Bson.Serialization.BsonSerializer")
                    .GetMethod("RegisterSerializer", [typeof(Type), Bson("MongoDB.Bson.Serialization.IBsonSerializer")])!,
                [typeof(Guid), serializer]);
        }

        /// <summary>Calls <c>ValueObjectBson.Register(fixture)</c>.</summary>
        public void Register() => Register([Fixture]);

        /// <summary>Calls <c>ValueObjectBson.Register(assemblies)</c>.</summary>
        public void Register(Assembly[] assemblies)
            => Call(Package().GetMethod("Register", [typeof(Assembly[])])!, [assemblies]);

        /// <summary>Calls <c>ValueObjectBson.Register&lt;TSelf, TValue&gt;()</c>.</summary>
        public void RegisterOne(string valueObject, Type valueType)
            => Call(
                Package().GetMethods().Single(method => method is { Name: "Register", IsGenericMethodDefinition: true })
                    .MakeGenericMethod(Fixture.GetType(valueObject, throwOnError: true)!, valueType),
                [false]);

        /// <summary>Gets the type of the serializer the driver gives a value object of the fixture.</summary>
        public Type SerializerOf(string valueObject) => SerializerOf(Fixture.GetType(valueObject, throwOnError: true)!);

        /// <summary>Gets the type of the serializer the driver gives a type, as serializing a document holding one does.</summary>
        public Type SerializerOf(Type type)
            => Call(Bson("MongoDB.Bson.Serialization.BsonSerializer").GetMethod("LookupSerializer", [typeof(Type)])!, [type])!.GetType();

        protected override Assembly? Load(AssemblyName assemblyName)
            => Fresh.Contains(assemblyName.Name!)
                ? LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll"))
                : null;

        private static object? Call(MethodInfo method, object?[] arguments)
            => method.Invoke(null, BindingFlags.DoNotWrapExceptions, binder: null, arguments, culture: null);

        private Type Bson(string name) => LoadFromAssemblyName(new AssemblyName("MongoDB.Bson")).GetType(name, throwOnError: true)!;

        private Type Package()
            => LoadFromAssemblyName(new AssemblyName("AdCodicem.ValueObjects.MongoDB"))
                .GetType("AdCodicem.ValueObjects.MongoDB.ValueObjectBson", throwOnError: true)!;
    }
}
