# Class ValueObjectHubProtocolExtensions {#AdCodicem_ValueObjects_MessagePack_ValueObjectHubProtocolExtensions}

Namespace: [AdCodicem.ValueObjects.MessagePack](AdCodicem.ValueObjects.MessagePack.md)  
Assembly: AdCodicem.ValueObjects.MessagePack.dll  

Wires value objects into SignalR's MessagePack hub protocol, on the server and on the .NET client alike.

```csharp
public static class ValueObjectHubProtocolExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectHubProtocolExtensions](AdCodicem.ValueObjects.MessagePack.ValueObjectHubProtocolExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### UseValueObjects\(MessagePackHubProtocolOptions, bool\) {#AdCodicem_ValueObjects_MessagePack_ValueObjectHubProtocolExtensions_UseValueObjects_Microsoft_AspNetCore_SignalR_MessagePackHubProtocolOptions_System_Boolean_}

Puts the value-object resolver in front of the hub protocol's own, keeping its security and its other settings.

```csharp
public static MessagePackHubProtocolOptions UseValueObjects(this MessagePackHubProtocolOptions options, bool trusted = false)
```

#### Parameters

`options` [MessagePackHubProtocolOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.signalr.messagepackhubprotocoloptions)

The hub protocol's options, as <code>AddMessagePackProtocol</code> hands them.

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether value objects are read without validation; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, the default, reads them through their
rules, as a hub reading what clients send should.

#### Returns

 [MessagePackHubProtocolOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.signalr.messagepackhubprotocoloptions)

The same options, so that calls can be chained.

#### Remarks

<p>
SignalR's own resolver falls back to contractless serialization, which writes a value object as an empty map,
<code>{}</code>, read back as the default instance, with nothing thrown. With this call, a value object travels as the
bare value of its underlying type, and an argument the value object refuses fails the invocation before the hub
method runs. Both ends need it: a client that does not call it writes <code>{}</code>, which a server that does refuses,
and a server that does not reads what such a client writes as the default instance.
</p>
<p>
<xref href="Microsoft.AspNetCore.SignalR.MessagePackHubProtocolOptions.SerializerOptions" data-throw-if-not-resolved="false"></xref> keeps
<xref href="MessagePack.MessagePackSecurity.UntrustedData" data-throw-if-not-resolved="false"></xref>, which SignalR sets, and under which MessagePack refuses a
dictionary keyed by a value object, or a set of value objects, for want of a hash-collision-resistant comparer of
the value object: key such a member by the underlying type, or give the options a security of the application's
own.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

