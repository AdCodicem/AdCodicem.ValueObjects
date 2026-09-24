# Enum ValueSetKind {#AdCodicem_ValueObjects_Annotations_ValueSetKind}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Whether a value object accepts arbitrary valid values or only an enumerated set.

```csharp
public enum ValueSetKind
```

## Fields

`Open = 0` 

Any value satisfying the declared rules is accepted. Declared known values are convenient constants only.



`Closed = 1` 

Only the values declared through <xref href="AdCodicem.ValueObjects.Annotations.KnownValueAttribute" data-throw-if-not-resolved="false"></xref> are accepted.

Membership is tested against a generated frozen lookup, and the known values are emitted as the
<code>enum</code> keyword of the OpenAPI schema. This is how reference-data codes are modelled without paying
for a real C# enumeration, which can carry neither validation nor a stable wire format.

