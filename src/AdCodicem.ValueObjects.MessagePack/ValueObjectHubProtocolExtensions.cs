using MessagePack;
using Microsoft.AspNetCore.SignalR;

namespace AdCodicem.ValueObjects.MessagePack;

/// <summary>
/// Wires value objects into SignalR's MessagePack hub protocol, on the server and on the .NET client alike.
/// </summary>
public static class ValueObjectHubProtocolExtensions
{
    /// <summary>
    /// Puts the value-object resolver in front of the hub protocol's own, keeping its security and its other settings.
    /// </summary>
    /// <param name="options">The hub protocol's options, as <c>AddMessagePackProtocol</c> hands them.</param>
    /// <param name="trusted">
    /// Whether value objects are read without validation; <see langword="false"/>, the default, reads them through their
    /// rules, as a hub reading what clients send should.
    /// </param>
    /// <returns>The same options, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// SignalR's own resolver falls back to contractless serialization, which writes a value object as an empty map,
    /// <c>{}</c>, read back as the default instance, with nothing thrown. With this call, a value object travels as the
    /// bare value of its underlying type, and an argument the value object refuses fails the invocation before the hub
    /// method runs. Both ends need it: a client that does not call it writes <c>{}</c>, which a server that does refuses,
    /// and a server that does not reads what such a client writes as the default instance.
    /// </para>
    /// <para>
    /// <see cref="MessagePackHubProtocolOptions.SerializerOptions"/> keeps
    /// <see cref="MessagePackSecurity.UntrustedData"/>, which SignalR sets, and under which MessagePack refuses a
    /// dictionary keyed by a value object, or a set of value objects, for want of a hash-collision-resistant comparer of
    /// the value object: key such a member by the underlying type, or give the options a security of the application's
    /// own.
    /// </para>
    /// </remarks>
    public static MessagePackHubProtocolOptions UseValueObjects(this MessagePackHubProtocolOptions options, bool trusted = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.SerializerOptions = options.SerializerOptions.WithValueObjects(trusted);
        return options;
    }
}
