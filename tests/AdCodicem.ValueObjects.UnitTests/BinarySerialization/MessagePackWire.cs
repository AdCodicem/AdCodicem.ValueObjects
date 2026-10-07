using System.Buffers;
using AdCodicem.ValueObjects.MessagePack;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// The option sets the MessagePack tests write and read under, and the plumbing that drives a formatter directly, as
/// MessagePack does, with no serializer around it.
/// </summary>
internal static class MessagePackWire
{
    /// <summary>
    /// Gets MessagePack's standard options: a <see cref="Guid"/> as text, a <see cref="decimal"/> as text, a
    /// <see cref="DateTime"/> as a timestamp.
    /// </summary>
    public static MessagePackSerializerOptions Standard => MessagePackSerializerOptions.Standard;

    /// <summary>
    /// Gets options whose resolver writes a <see cref="Guid"/>, a <see cref="decimal"/> and a <see cref="DateTime"/> in
    /// their binary form, before the standard resolver.
    /// </summary>
    public static MessagePackSerializerOptions Native { get; } = MessagePackSerializerOptions.Standard.WithResolver(
        CompositeResolver.Create(
            NativeGuidResolver.Instance,
            NativeDecimalResolver.Instance,
            NativeDateTimeResolver.Instance,
            StandardResolver.Instance));

    /// <summary>
    /// Gets the standard options with the value objects' strict resolver in front.
    /// </summary>
    public static MessagePackSerializerOptions Strict { get; } = MessagePackSerializerOptions.Standard.WithValueObjects();

    /// <summary>
    /// Gets the standard options with the value objects' trusted resolver in front.
    /// </summary>
    public static MessagePackSerializerOptions Trusting { get; } =
        MessagePackSerializerOptions.Standard.WithValueObjects(trusted: true);

    /// <summary>
    /// Writes a value as MessagePack does, under the standard options unless others are given.
    /// </summary>
    /// <typeparam name="T">The type written.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="options">The options, the standard ones by default.</param>
    /// <returns>The bytes written.</returns>
    public static byte[] Bytes<T>(T value, MessagePackSerializerOptions? options = null)
        => MessagePackSerializer.Serialize(value, options ?? Standard);

    /// <summary>
    /// Writes a value through a formatter, with no serializer around it.
    /// </summary>
    /// <typeparam name="T">The type written.</typeparam>
    /// <param name="formatter">The formatter.</param>
    /// <param name="value">The value.</param>
    /// <param name="options">The options the formatter is handed.</param>
    /// <returns>The bytes written.</returns>
    public static byte[] Write<T>(IMessagePackFormatter<T> formatter, T value, MessagePackSerializerOptions options)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        formatter.Serialize(ref writer, value, options);
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Reads a value through a formatter, with no serializer around it.
    /// </summary>
    /// <typeparam name="T">The type read.</typeparam>
    /// <param name="formatter">The formatter.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="options">The options the formatter is handed.</param>
    /// <returns>What the formatter read.</returns>
    public static T Read<T>(IMessagePackFormatter<T> formatter, byte[] bytes, MessagePackSerializerOptions options)
    {
        var reader = new MessagePackReader(bytes);

        return formatter.Deserialize(ref reader, options);
    }

    /// <summary>
    /// Reads the code of the rule an exception, or one it wraps, carries.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The code, or <see langword="null"/>.</returns>
    public static string? Code(Exception exception) => ValueObjectErrors.TryGetCode(exception, out var code) ? code : null;
}
