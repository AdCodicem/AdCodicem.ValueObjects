using ProtoBuf;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// Fixes, for a closed <see cref="ValueObjectProtoSerializer{TSelf, TValue, TProfile}"/>, whether reads skip validation and
/// which of protobuf-net's inbuilt serializers carries the value.
/// </summary>
/// <remarks>
/// protobuf-net creates a serializer named by its type with its parameterless constructor, and keeps one per type for the
/// whole process, so what a serializer does is fixed by its type arguments rather than by arguments to a constructor.
/// <see cref="ValueObjectProtoProfiles"/> holds the six combinations; a profile of your own implements this interface.
/// </remarks>
public interface IValueObjectProtoProfile
{
    /// <summary>
    /// Gets whether a read goes through <c>CreateUnchecked</c>, for bytes the application alone wrote, rather than through
    /// <c>TryCreate</c>.
    /// </summary>
    static abstract bool Trusted { get; }

    /// <summary>
    /// Gets the level whose inbuilt serializer of the underlying type the value goes through, which decides how a
    /// <see cref="System.Guid"/>, a <see cref="decimal"/>, a <see cref="System.DateTime"/> and a
    /// <see cref="System.TimeSpan"/> are written.
    /// </summary>
    static abstract CompatibilityLevel CompatibilityLevel { get; }
}
