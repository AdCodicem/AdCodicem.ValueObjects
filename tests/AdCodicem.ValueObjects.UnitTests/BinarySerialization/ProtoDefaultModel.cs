using AdCodicem.ValueObjects.ProtobufNet;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// The one registration of the suite's value objects on <see cref="RuntimeTypeModel.Default"/>, strict, which
/// <c>Serializer.*</c> and protobuf-net.Grpc's default binder read.
/// </summary>
/// <remarks>
/// The default model is process-wide, and protobuf-net freezes a type there once it serializes one: a value object the
/// default model met before this registration would keep whatever protobuf-net made of it for the rest of the run. Every
/// test that goes through the default model calls <see cref="EnsureRegistered"/> first; every other test builds a model
/// of its own.
/// </remarks>
internal static class ProtoDefaultModel
{
    private static readonly Lazy<RuntimeTypeModel> Registered = new(
        () => RuntimeTypeModel.Default.AddValueObjects(typeof(Iban).Assembly),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Registers the suite's value objects on the default model, once.</summary>
    /// <returns>The default model.</returns>
    public static RuntimeTypeModel EnsureRegistered() => Registered.Value;
}
