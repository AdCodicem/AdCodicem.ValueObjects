using ProtoBuf;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// The plumbing the protobuf-net tests write and read through: a fresh model at a level, and the bytes a model writes
/// for a value and reads back from them.
/// </summary>
/// <remarks>
/// A model of its own per test, never <see cref="RuntimeTypeModel.Default"/>, which is process-wide and freezes each type
/// once it serializes one: the tests that need the default model go through <see cref="ProtoDefaultModel"/>.
/// </remarks>
internal static class ProtoWire
{
    /// <summary>Gets the three compatibility levels a profile names.</summary>
    public static IReadOnlyList<CompatibilityLevel> Levels { get; } =
        [CompatibilityLevel.Level200, CompatibilityLevel.Level240, CompatibilityLevel.Level300];

    /// <summary>
    /// Creates a model of its own, at a level, which protobuf-net lets nobody change once a type is added.
    /// </summary>
    /// <param name="level">The level, protobuf-net's default unless another is given.</param>
    /// <returns>The model.</returns>
    public static RuntimeTypeModel Model(CompatibilityLevel level = CompatibilityLevel.Level200)
    {
        var model = RuntimeTypeModel.Create();
        model.DefaultCompatibilityLevel = level;
        return model;
    }

    /// <summary>Writes a value as a model writes it.</summary>
    /// <typeparam name="T">The type written.</typeparam>
    /// <param name="model">The model.</param>
    /// <param name="value">The value.</param>
    /// <returns>The bytes written.</returns>
    public static byte[] Bytes<T>(TypeModel model, T value)
    {
        using var stream = new MemoryStream();
        model.Serialize(stream, value);
        return stream.ToArray();
    }

    /// <summary>Reads a value as a model reads it.</summary>
    /// <typeparam name="T">The type read.</typeparam>
    /// <param name="model">The model.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>What the model read.</returns>
    public static T Read<T>(TypeModel model, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return model.Deserialize<T>(stream);
    }

    /// <summary>Writes the bytes as hexadecimal, for a message.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    public static string Hex(byte[] bytes) => Convert.ToHexString(bytes);
}

/// <summary>A message of one member, field 1, over a value object or the primitive it replaces.</summary>
/// <typeparam name="T">The member's type.</typeparam>
[ProtoContract]
public sealed class Holder<T>
{
    [ProtoMember(1)]
    public T? Value { get; set; }
}
