# Namespace AdCodicem.ValueObjects.MessagePack {#AdCodicem_ValueObjects_MessagePack}

### Classes

 [ValueObjectFormatter<TSelf, TValue\>](AdCodicem.ValueObjects.MessagePack.ValueObjectFormatter\-2.md)

Writes a value object as the bare value the options' formatter of its underlying type writes, and reads it back
through the value object's rules.

 [ValueObjectHubProtocolExtensions](AdCodicem.ValueObjects.MessagePack.ValueObjectHubProtocolExtensions.md)

Wires value objects into SignalR's MessagePack hub protocol, on the server and on the .NET client alike.

 [ValueObjectMessagePackExtensions](AdCodicem.ValueObjects.MessagePack.ValueObjectMessagePackExtensions.md)

Wires value objects into MessagePack serializer options.

 [ValueObjectResolver](AdCodicem.ValueObjects.MessagePack.ValueObjectResolver.md)

Answers the formatter of any value object, closed over it at compile time through the descriptor's visitor, and
nothing else.

