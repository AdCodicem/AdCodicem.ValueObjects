# Class ValueObjectJsonSerializerSettingsExtensions {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectJsonSerializerSettingsExtensions}

Namespace: [AdCodicem.ValueObjects.NewtonsoftJson](AdCodicem.ValueObjects.NewtonsoftJson.md)  
Assembly: AdCodicem.ValueObjects.NewtonsoftJson.dll  

Wires value object support into <xref href="Newtonsoft.Json.JsonSerializerSettings" data-throw-if-not-resolved="false"></xref>.

```csharp
public static class ValueObjectJsonSerializerSettingsExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectJsonSerializerSettingsExtensions](AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectJsonSerializerSettingsExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjects\(JsonSerializerSettings, bool\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectJsonSerializerSettingsExtensions_AddValueObjects_Newtonsoft_Json_JsonSerializerSettings_System_Boolean_}

Adds <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter" data-throw-if-not-resolved="false"></xref> to the settings, with the two settings it reads value objects best under.

```csharp
public static JsonSerializerSettings AddValueObjects(this JsonSerializerSettings settings, bool decimalReals = true)
```

#### Parameters

`settings` JsonSerializerSettings

Settings to configure.

`decimalReals` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> to set <xref href="Newtonsoft.Json.FloatParseHandling.Decimal" data-throw-if-not-resolved="false"></xref>; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> to leave
<xref href="Newtonsoft.Json.JsonSerializerSettings.FloatParseHandling" data-throw-if-not-resolved="false"></xref> as the settings have it.

#### Returns

 JsonSerializerSettings

The same settings, so calls can be chained.

#### Remarks

<p>
Newtonsoft.Json reads each token under its settings before a converter sees it. This sets
<xref href="Newtonsoft.Json.DateParseHandling.None" data-throw-if-not-resolved="false"></xref>, so that a string that looks like a date reaches the converter as the text
it is, rather than as a date converted to local time, which a string or a <xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref> value
object cannot be read back from.
</p>
<p>
<xref href="Newtonsoft.Json.FloatParseHandling.Decimal" data-throw-if-not-resolved="false"></xref> keeps every digit of a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> value object, where the
default reads it through a <xref href="System.Double" data-throw-if-not-resolved="false"></xref>: <code>12.50</code> would read back as 12.5, and
<code>1234567890123456789.12</code> as 1234567890123456800. It applies to every number of the payload, so pass
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> when a payload carries a <xref href="System.Double" data-throw-if-not-resolved="false"></xref> or a <xref href="System.Single" data-throw-if-not-resolved="false"></xref> beyond the range
of a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, about ±7.9 × 10^28, which makes the reader throw under it, or one smaller than its 28
decimal places, which loses the digits past them.
</p>
<p>
The converter is added once: a registration already in the settings is left alone.
</p>
<p>
It leaves <xref href="Newtonsoft.Json.JsonSerializerSettings.ContractResolver" data-throw-if-not-resolved="false"></xref> as it is: <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectContractResolver" data-throw-if-not-resolved="false"></xref>
is for code that hands Newtonsoft.Json a contract resolver alone.
</p>

