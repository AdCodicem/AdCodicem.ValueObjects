using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Orleans;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Orleans.Serialization;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// Grain calls through an in-process cluster whose silo and client both call <c>AddValueObjects()</c>: value objects as
/// arguments and results, nullable and typed <see cref="object"/>, a refused request and a refused reply reaching the
/// caller with their codes, a default refused before it is sent, and a <see cref="ValueObjectException"/> a grain lets
/// escape arriving with its code. A test needing another configuration starts a cluster of its own.
/// </summary>
/// <param name="cluster">The cluster.</param>
public sealed class OrleansGrainCallTests(GrainCluster cluster) : IClassFixture<GrainCluster>
{
    /// <summary>
    /// Every value object of the domain crosses a grain call as itself, nullable and typed <see cref="object"/>.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    /// <returns>The test.</returns>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public Task Every_value_object_crosses_a_grain_call_as_itself(string type)
        => Samples.All[type].CrossesAGrainCallAsItselfAsync(cluster.Grain());

    [Fact]
    public async Task Value_objects_are_arguments_and_results_of_a_grain_method()
    {
        var grain = cluster.Grain();

        (await grain.Add(GrainSku.Create("abc-123"), GrainQuantity.Create(3))).Should().Be(GrainQuantity.Create(3));
        (await grain.Add(GrainSku.Create("abc-123"), GrainQuantity.Create(4))).Should().Be(GrainQuantity.Create(7));
        (await grain.Maybe(null)).Should().BeNull();
        (await grain.Maybe(GrainQuantity.Create(4))).Should().Be(GrainQuantity.Create(4));
        (await grain.EchoObject(GrainSku.Create("abc-123"))).Should().Be(GrainSku.Create("ABC-123"));
        (await grain.EchoObject(AliasedCode.Create("ab"))).Should().Be(AliasedCode.Create("ab"));
        (await grain.Echo(new GrainOrder { Sku = GrainSku.Create("abc-123"), Quantity = GrainQuantity.Create(2), Amount = GrainAmount.Create(1m) }))
            .Should().BeEquivalentTo(new GrainOrder { Sku = GrainSku.Create("ABC-123"), Quantity = GrainQuantity.Create(2), Amount = GrainAmount.Create(1m) });
        (await grain.Echo(new GrainInterfaceBox { Comparable = GrainQuantity.Create(5), Any = GrainSku.Create("abc-123") }))
            .Should().BeEquivalentTo(new GrainInterfaceBox { Comparable = GrainQuantity.Create(5), Any = GrainSku.Create("ABC-123") });
    }

    /// <summary>
    /// One text met twice in a call, in a member and a nullable member made from one literal, or twice in a list of
    /// nullable value objects, crosses it: the codec writes it in full each time.
    /// </summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_value_object_met_twice_in_a_call_crosses_it()
    {
        var grain = cluster.Grain();
        var order = new GrainOrder { Sku = GrainSku.Create("ABC-123"), Quantity = GrainQuantity.Create(1), Optional = GrainSku.Create("ABC-123") };

        (await grain.Echo(order)).Should().BeEquivalentTo(order);
        (await grain.Echo(new List<GrainSku?> { order.Sku, order.Sku })).Should().Equal(order.Sku, order.Sku);
    }

    [Fact]
    public async Task A_construction_of_a_generic_value_object_and_one_written_by_hand_cross_a_grain_call()
    {
        var grain = cluster.Grain();

        (await grain.Echo(Reference<OrleansCallOwner>.Create("po-9"))).Should().Be(Reference<OrleansCallOwner>.Create("PO-9"));
        (await grain.Echo<object>(Reference<OrleansCallOwner>.Create("po-9"))).Should().Be(Reference<OrleansCallOwner>.Create("PO-9"));
        (await grain.Echo(Declared<OrleansCallDeclaration, string>.Create("ab"))).Should().Be(Declared<OrleansCallDeclaration, string>.Create("ab"));
    }

    /// <summary>
    /// A request holding a value the silo's rules refuse fails on the caller with the silo's refusal and its code; the
    /// grain is not invoked, and serves the next call.
    /// </summary>
    [Fact]
    public async Task A_refused_request_reaches_the_caller_with_its_code_and_the_grain_is_not_invoked()
    {
        var grain = cluster.Grain();

        var refusal = (await FluentActions.Awaiting(() => grain.Add(GrainSku.Create("abc-123"), Unchecked.Of<GrainQuantity, int>(500)))
            .Should().ThrowAsync<SerializerException>()).Which;
        refusal.Message.Should().Be("The value read is not a valid GrainQuantity: The value must be less than or equal to 100.");
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);

        (await grain.Add(GrainSku.Create("abc-123"), GrainQuantity.Create(3))).Should().Be(GrainQuantity.Create(3), "the refused call added nothing");
    }

    [Fact]
    public async Task A_refused_reply_fails_on_the_caller_with_its_code()
    {
        var refusal = (await FluentActions.Awaiting(() => cluster.Grain().Forged()).Should().ThrowAsync<SerializerException>()).Which;

        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task A_default_its_type_refuses_is_refused_before_it_is_sent()
    {
#pragma warning disable VO0010 // the default instance, which its rules refuse, on purpose
        var refusal = (await FluentActions.Awaiting(() => cluster.Grain().Accept(default)).Should().ThrowAsync<SerializerException>()).Which;
#pragma warning restore VO0010

        refusal.Message.Should().Be("The value to write is not a valid GrainQuantity: The value must be greater than or equal to 1.");
        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A call between two grains of one silo copies its arguments rather than serializing them, so a default instance its
    /// type refuses arrives there.
    /// </summary>
    [Fact]
    public async Task A_default_reaches_a_grain_of_the_same_silo_through_the_copy()
    {
        var answer = await cluster.Grain().RelayDefault(Guid.NewGuid());

        answer.Should().Contain("accepted 0 (IsDefault=True)");
    }

    [Fact]
    public async Task A_value_object_exception_a_grain_lets_escape_reaches_the_caller_with_its_code()
    {
        var grain = cluster.Grain();

        (await grain.CreateInside(5)).Should().Be("created 5");
        var refusal = (await FluentActions.Awaiting(() => grain.CreateInside(500)).Should().ThrowAsync<ValueObjectException>()).Which;
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        refusal.ValueObjectType.Should().Be<GrainQuantity>();
        refusal.AttemptedValue.Should().BeNull();

        var subclass = (await FluentActions.Awaiting(() => grain.ThrowSubclass()).Should().ThrowExactlyAsync<ValueObjectException>()).Which;
        subclass.ErrorCode.Should().Be("fixture.custom");
        subclass.AttemptedValue.Should().BeNull();
    }

    /// <summary>
    /// With the package on the client alone, the silo has no codec for a value object of its grain interfaces: in the
    /// Development environment, which a test cluster runs in, Orleans' check of those interfaces fails the silo's start;
    /// elsewhere, the silo's own failure reaches the caller of the first call.
    /// </summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Without_the_package_on_the_silo_the_silo_finds_no_codec()
    {
        await FluentActions.Awaiting(() => GrainCluster.StartAsync(1, static _ => { }, static client => client.AddValueObjects()))
            .Should().ThrowAsync<CodecNotFoundException>().WithMessage("Could not find a codec for type AdCodicem.ValueObjects.Fixtures.Grains.*");

        await using var lenient = await GrainCluster.StartAsync(
            1,
            static silo => silo.Configure(static options => options.EnableConfigurationAnalysis = false),
            static client => client.AddValueObjects());
        var grain = lenient.Client.GetGrain<IValueObjectGrain>(Guid.NewGuid());
        await FluentActions.Awaiting(() => grain.Add(GrainSku.Create("abc-123"), GrainQuantity.Create(3)))
            .Should().ThrowAsync<CodecNotFoundException>().WithMessage("Could not find a codec for type AdCodicem.ValueObjects.Fixtures.Grains.GrainSku.");
        await FluentActions.Awaiting(() => grain.EchoObject(GrainSku.Create("abc-123")))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Type \"AdCodicem.ValueObjects.Fixtures.Grains.GrainSku\" is not allowed.*");
    }

    /// <summary>
    /// Between two silos a value object travels through both serializers: a value is carried, a forged one is refused by
    /// the silo receiving it, its code relayed to the client, and a default its type refuses is refused by the silo
    /// sending it. A client registering its value objects by type receives an exception for one it did not register,
    /// its code intact.
    /// </summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Between_two_silos_a_value_object_is_written_and_read_through_its_rules()
    {
        await using var twoSilos = await GrainCluster.StartAsync(
            2,
            static silo => silo.AddValueObjects(),
            static client => client.AddValueObject<GrainSku, string>().AddValueObject<GrainQuantity, int>());

        var keys = new Dictionary<string, Guid>();
        for (var attempt = 0; attempt < 200 && keys.Count < 2; attempt++)
        {
            var key = Guid.NewGuid();
            keys.TryAdd(await twoSilos.Client.GetGrain<IValueObjectGrain>(key).Where(), key);
        }

        keys.Should().HaveCount(2, "two grains run on two silos");
        var first = twoSilos.Client.GetGrain<IValueObjectGrain>(keys.Values.First());
        var other = keys.Values.Last();

        (await first.Relay(other, GrainQuantity.Create(5))).Should().EndWith($"accepted 5 (IsDefault=False) on {keys.Keys.Last()}");
        var forged = (await FluentActions.Awaiting(() => first.RelayForged(other)).Should().ThrowAsync<SerializerException>()).Which;
        forged.Message.Should().StartWith("The value read is not a valid GrainQuantity");
        ValueObjectErrors.TryGetCode(forged, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange, "the receiving silo's code travels back through the relaying grain");
        (await FluentActions.Awaiting(() => first.RelayDefault(other)).Should().ThrowAsync<SerializerException>())
            .Which.Message.Should().StartWith("The value to write is not a valid GrainQuantity");

        var thrown = (await FluentActions.Awaiting(() => first.ThrowForQuantity()).Should().ThrowAsync<ValueObjectException>()).Which;
        thrown.ErrorCode.Should().Be("value_object.test");
        (await first.EchoObject(GrainQuantity.Create(4))).Should().Be(GrainQuantity.Create(4));
    }
}

/// <summary>The owner of the references a grain call carries, which nothing else names.</summary>
public sealed class OrleansCallOwner;

/// <summary>A code written by hand that a grain call carries, which nothing else declares.</summary>
public sealed class OrleansCallDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };
}
