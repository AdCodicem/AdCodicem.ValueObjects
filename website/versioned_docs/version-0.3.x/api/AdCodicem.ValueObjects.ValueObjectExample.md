# Class ValueObjectExample {#AdCodicem_ValueObjects_ValueObjectExample}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A generic bridge to the example a value object declares through <xref href="AdCodicem.ValueObjects.IValueObjectExample%601" data-throw-if-not-resolved="false"></xref>, used by
generated code.

```csharp
public static class ValueObjectExample
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectExample](AdCodicem.ValueObjects.ValueObjectExample.md)

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
it. Named on the type, <code>CountryCode.Example</code> would miss an explicit implementation and fail to compile in a file
the consumer cannot edit.

## Methods

### Of<TSelf\>\(\) {#AdCodicem_ValueObjects_ValueObjectExample_Of__1}

Gets the example a value object declares.

```csharp
public static TSelf Of<TSelf>() where TSelf : IValueObjectExample<TSelf>
```

#### Returns

 TSelf

Its example.

#### Type Parameters

`TSelf` 

The value object.

