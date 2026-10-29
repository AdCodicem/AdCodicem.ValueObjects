# Namespace AdCodicem.ValueObjects.ProtobufNet {#AdCodicem_ValueObjects_ProtobufNet}

### Classes

 [ValueObjectProtoProfiles.StrictLevel200](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.StrictLevel200.md)

Reads through the rules, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level200" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectProtoProfiles.StrictLevel240](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.StrictLevel240.md)

Reads through the rules, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level240" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectProtoProfiles.StrictLevel300](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.StrictLevel300.md)

Reads through the rules, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level300" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectProtoProfiles.TrustedLevel200](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.TrustedLevel200.md)

Reads without validating, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level200" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectProtoProfiles.TrustedLevel240](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.TrustedLevel240.md)

Reads without validating, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level240" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectProtoProfiles.TrustedLevel300](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.TrustedLevel300.md)

Reads without validating, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level300" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectBinderConfiguration](AdCodicem.ValueObjects.ProtobufNet.ValueObjectBinderConfiguration.md)

Builds the protobuf-net.Grpc binder configuration of a code-first server whose messages hold value objects.

 [ValueObjectProtoModelExtensions](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoModelExtensions.md)

Registers the serializers of value objects on a protobuf-net <xref href="ProtoBuf.Meta.RuntimeTypeModel" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectProtoProfiles](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.md)

The profiles the package ships: strict or trusted, at each of protobuf-net's compatibility levels.

 [ValueObjectProtoSerializer<TSelf, TValue, TProfile\>](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer\-3.md)

Writes a value object as protobuf-net writes its underlying type at the level <code class="typeparamref">TProfile</code> names,
and reads it back through its rules unless the profile trusts the bytes.

 [ValueObjectProtoSerializer<TSelf, TValue\>](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer\-2.md)

Writes a value object as protobuf-net writes its underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level200" data-throw-if-not-resolved="false"></xref>, and
reads it back through its rules.

### Interfaces

 [IValueObjectProtoProfile](AdCodicem.ValueObjects.ProtobufNet.IValueObjectProtoProfile.md)

Fixes, for a closed <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer%603" data-throw-if-not-resolved="false"></xref>, whether reads skip validation and
which of protobuf-net's inbuilt serializers carries the value.

