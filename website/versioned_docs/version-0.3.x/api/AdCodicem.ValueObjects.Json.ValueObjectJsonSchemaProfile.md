# Enum ValueObjectJsonSchemaProfile {#AdCodicem_ValueObjects_Json_ValueObjectJsonSchemaProfile}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Chooses whom a JSON Schema exported through <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonSchema" data-throw-if-not-resolved="false"></xref> describes a value object for.

```csharp
public enum ValueObjectJsonSchemaProfile
```

## Fields

`OpenApi = 0` 

What the serializer reads and writes under the options, with the formats OpenAPI uses: the default.

A number the options let be read or written as text is a number or a string held to the form a number is written
in, as System.Text.Json documents the number alone, and a real under
<xref href="System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals" data-throw-if-not-resolved="false"></xref> lists the named
literals its bounds let through. Every <code>format</code> the type declares is kept: <code>int32</code>, <code>decimal</code> or
<code>iban</code> as well as <code>uuid</code> or <code>date</code>.

`LanguageModel = 1` 

What a language model should send: the underlying type alone, with any format JSON Schema does not define moved
into the description.

A number is a number, whatever the options let the serializer read, as a tool's schema describes a bare number,
and its example and known values are written as numbers. A <code>format</code> JSON Schema defines, such as
<code>uuid</code>, <code>date</code> or <code>date-time</code>, stays; any other, such as <code>int32</code>, <code>decimal</code> or
<code>iban</code>, which a model or its provider may not know or may refuse, becomes a sentence of the description. A
closed set lists its values in <code>enum</code> alone, as under the OpenAPI profile: the description does not repeat
them.

