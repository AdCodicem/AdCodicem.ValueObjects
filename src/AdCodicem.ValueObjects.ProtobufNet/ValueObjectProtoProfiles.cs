using ProtoBuf;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// The profiles the package ships: strict or trusted, at each of protobuf-net's compatibility levels.
/// </summary>
/// <remarks>
/// Each is a type argument, never an instance. Name one in <see cref="ValueObjectProtoSerializer{TSelf, TValue, TProfile}"/>:
/// <c>[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer&lt;OrderId, Guid, ValueObjectProtoProfiles.StrictLevel300&gt;))]</c>.
/// </remarks>
public static class ValueObjectProtoProfiles
{
    /// <summary>Reads through the rules, and writes as protobuf-net writes the underlying type at <see cref="CompatibilityLevel.Level200"/>.</summary>
    public sealed class StrictLevel200 : IValueObjectProtoProfile
    {
        /// <inheritdoc />
        public static bool Trusted => false;

        /// <inheritdoc />
        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level200;
    }

    /// <summary>Reads through the rules, and writes as protobuf-net writes the underlying type at <see cref="CompatibilityLevel.Level240"/>.</summary>
    public sealed class StrictLevel240 : IValueObjectProtoProfile
    {
        /// <inheritdoc />
        public static bool Trusted => false;

        /// <inheritdoc />
        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level240;
    }

    /// <summary>Reads through the rules, and writes as protobuf-net writes the underlying type at <see cref="CompatibilityLevel.Level300"/>.</summary>
    public sealed class StrictLevel300 : IValueObjectProtoProfile
    {
        /// <inheritdoc />
        public static bool Trusted => false;

        /// <inheritdoc />
        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level300;
    }

    /// <summary>Reads without validating, and writes as protobuf-net writes the underlying type at <see cref="CompatibilityLevel.Level200"/>.</summary>
    public sealed class TrustedLevel200 : IValueObjectProtoProfile
    {
        /// <inheritdoc />
        public static bool Trusted => true;

        /// <inheritdoc />
        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level200;
    }

    /// <summary>Reads without validating, and writes as protobuf-net writes the underlying type at <see cref="CompatibilityLevel.Level240"/>.</summary>
    public sealed class TrustedLevel240 : IValueObjectProtoProfile
    {
        /// <inheritdoc />
        public static bool Trusted => true;

        /// <inheritdoc />
        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level240;
    }

    /// <summary>Reads without validating, and writes as protobuf-net writes the underlying type at <see cref="CompatibilityLevel.Level300"/>.</summary>
    public sealed class TrustedLevel300 : IValueObjectProtoProfile
    {
        /// <inheritdoc />
        public static bool Trusted => true;

        /// <inheritdoc />
        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level300;
    }
}
