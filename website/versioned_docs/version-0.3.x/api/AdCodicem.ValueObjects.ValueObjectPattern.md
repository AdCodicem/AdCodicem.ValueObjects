# Class ValueObjectPattern {#AdCodicem_ValueObjects_ValueObjectPattern}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A generic bridge to the pattern a value object declares through <xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref>,
used by generated code.

```csharp
public static class ValueObjectPattern
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectPattern](AdCodicem.ValueObjects.ValueObjectPattern.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

A static abstract member is reached through a type parameter, which finds it however the value object implements
it. Named on the type, <code>Iban.Pattern</code> would miss an explicit implementation and fail to compile in a file the
consumer cannot edit. The JIT specializes the call for the struct, so the indirection costs nothing.

## Methods

### Of<TSelf\>\(\) {#AdCodicem_ValueObjects_ValueObjectPattern_Of__1}

Gets the regular expression a value object declares.

```csharp
public static Regex Of<TSelf>() where TSelf : IValueObjectPatternValidator
```

#### Returns

 [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex)

Its pattern.

#### Type Parameters

`TSelf` 

The value object.

