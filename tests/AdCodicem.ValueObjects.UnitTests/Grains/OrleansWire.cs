using System.Buffers;
using AdCodicem.ValueObjects.Orleans;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;
using Orleans.Serialization.Buffers;
using Orleans.Serialization.Session;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// Orleans serializers built as a silo or a client builds its own, the one process-wide registration aside, and bytes
/// written field by field for what no serializer writes.
/// </summary>
/// <remarks>
/// A serializer is a service provider of its own, so each configuration builds one: none is process-wide, and a test
/// needing another trust or another registration builds its own through <see cref="Build"/>.
/// </remarks>
public static class OrleansWire
{
    /// <summary>Gets a serializer reading every value object the registry resolves through its rules.</summary>
    public static Serializer Strict { get; } = Build(static serializer => serializer.AddValueObjects());

    /// <summary>Gets a serializer reading every value object the registry resolves without validating it.</summary>
    public static Serializer Trusted { get; } = Build(static serializer => serializer.AddValueObjects(trusted: true));

    /// <summary>Gets a serializer without the package, as an application without it builds one.</summary>
    public static Serializer Bare { get; } = Build(static _ => { });

    /// <summary>
    /// Builds a serializer the way a silo or a client does.
    /// </summary>
    /// <param name="configure">What the application adds to the serializer.</param>
    /// <returns>The serializer.</returns>
    public static Serializer Build(Action<ISerializerBuilder> configure)
        => Services(configure).GetRequiredService<Serializer>();

    /// <summary>
    /// Builds the services of a serializer the way a silo or a client does.
    /// </summary>
    /// <param name="configure">What the application adds to the serializer.</param>
    /// <returns>The services.</returns>
    public static ServiceProvider Services(Action<ISerializerBuilder> configure)
        => new ServiceCollection().AddSerializer(configure).BuildServiceProvider();

    /// <summary>
    /// Reads an object Orleans wrote, which a test expects there.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="serializer">The serializer reading it.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The object.</returns>
    public static T Read<T>(this Serializer serializer, byte[] bytes)
        where T : class
        => serializer.Deserialize<T>(bytes) ?? throw new InvalidOperationException("The bytes hold a null reference.");

    /// <summary>
    /// Writes bytes field by field, through a session of <see cref="Strict"/>'s services, for a value no serializer writes:
    /// an object missing a field, or holding one no codec writes.
    /// </summary>
    /// <param name="write">What to write.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Craft(Write write)
    {
        using var session = CraftingServices.GetRequiredService<SerializerSessionPool>().GetSession();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = Writer.Create(buffer, session);
        write(ref writer);
        writer.Commit();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Writes fields with the writer handed in.</summary>
    /// <param name="writer">The writer.</param>
    public delegate void Write(ref Writer<ArrayBufferWriter<byte>> writer);

    private static ServiceProvider CraftingServices { get; } = Services(static serializer => serializer.AddValueObjects());
}
