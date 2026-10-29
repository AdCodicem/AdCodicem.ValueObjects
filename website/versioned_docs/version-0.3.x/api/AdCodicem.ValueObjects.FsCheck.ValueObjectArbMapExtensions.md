# Class ValueObjectArbMapExtensions {#AdCodicem_ValueObjects_FsCheck_ValueObjectArbMapExtensions}

Namespace: [AdCodicem.ValueObjects.FsCheck](AdCodicem.ValueObjects.FsCheck.md)  
Assembly: AdCodicem.ValueObjects.FsCheck.dll  

Merges the arbitraries of value objects into an FsCheck map, one per type, so that the map derives the types that
hold them too.

```csharp
public static class ValueObjectArbMapExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectArbMapExtensions](AdCodicem.ValueObjects.FsCheck.ValueObjectArbMapExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Each value object is merged with <code>MergeArb</code> on its own arbitrary, <xref href="AdCodicem.ValueObjects.FsCheck.ValueObjectArbitrary.For%60%602(AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions)" data-throw-if-not-resolved="false"></xref>,
closed over it through its descriptor's visitor: a single factory constrained to value objects would break the
derivation of every other type the map serves.

## Methods

### MergeValueObject<TSelf, TValue\>\(IArbMap, ValueObjectSamplerOptions?\) {#AdCodicem_ValueObjects_FsCheck_ValueObjectArbMapExtensions_MergeValueObject__2_FsCheck_IArbMap_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Merges the arbitrary of one value object: a construction of a generic one, or one written by hand that nothing
registers.

```csharp
public static IArbMap MergeValueObject<TSelf, TValue>(this IArbMap map, ValueObjectSamplerOptions? options = null) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`map` IArbMap

The map to merge into.

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)?

How each value is drawn, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the defaults.

#### Returns

 IArbMap

The merged map.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">map</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### MergeValueObjects\(IArbMap, Assembly, ValueObjectSamplerOptions?\) {#AdCodicem_ValueObjects_FsCheck_ValueObjectArbMapExtensions_MergeValueObjects_FsCheck_IArbMap_System_Reflection_Assembly_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Merges the arbitrary of every value object the generated registration of an assembly registers, and of every other
value object of that assembly the registry holds by then but a construction of a generic one.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of the assembly by its metadata.")]
public static IArbMap MergeValueObjects(this IArbMap map, Assembly assembly, ValueObjectSamplerOptions? options = null)
```

#### Parameters

`map` IArbMap

The map to merge into: <code>ArbMap.Default</code>, or one already merged.

`assembly` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)

The assembly declaring the value objects: <code>typeof(Iban).Assembly</code>.

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)?

How each value is drawn, the generators of the rules no schema carries included, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the defaults.

#### Returns

 IArbMap

The merged map.

#### Remarks

The constructions of a generic value object, <code>Reference&lt;PurchaseOrder&gt;</code> for one, are not merged, even one
the registry described by then, and neither is a value object written by hand that nothing registered or resolved:
merge each with <xref href="AdCodicem.ValueObjects.FsCheck.ValueObjectArbMapExtensions.MergeValueObject%60%602(FsCheck.IArbMap%2cAdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions)" data-throw-if-not-resolved="false"></xref>. Without it, FsCheck refuses the type, and every type
holding it, with its own message, "not handled automatically", naming it. The assembly is not searched through the
registry's <code>TryResolve</code>, which would describe and keep every value object written by hand it declares.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">map</code> or <code class="paramref">assembly</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

