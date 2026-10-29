# Namespace AdCodicem.ValueObjects.Json {#AdCodicem_ValueObjects_Json}

### Classes

 [JsonSerializerOptionsExtensions](AdCodicem.ValueObjects.Json.JsonSerializerOptionsExtensions.md)

Wires value object support into <xref href="System.Text.Json.JsonSerializerOptions" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectJsonConverter<TSelf, TValue\>](AdCodicem.ValueObjects.Json.ValueObjectJsonConverter\-2.md)

Serializes a value object as its bare underlying value, delegating the underlying value to
System.Text.Json itself.

 [ValueObjectJsonConverterFactory](AdCodicem.ValueObjects.Json.ValueObjectJsonConverterFactory.md)

Supplies the converter of any value object, so that a value object always serializes as its bare underlying
value.

 [ValueObjectJsonRegistry](AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry.md)

Directory of the converters generated for the value objects of the loaded assemblies.

 [ValueObjectJsonSchema](AdCodicem.ValueObjects.Json.ValueObjectJsonSchema.md)

Describes a value object in a JSON Schema exported by System.Text.Json as its underlying value, carrying the rules
declared on it.

### Enums

 [ValueObjectJsonSchemaProfile](AdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile.md)

Chooses whom a JSON Schema exported through <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonSchema" data-throw-if-not-resolved="false"></xref> describes a value object for.

