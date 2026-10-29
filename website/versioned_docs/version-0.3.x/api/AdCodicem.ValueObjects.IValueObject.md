# Interface IValueObject {#AdCodicem_ValueObjects_IValueObject}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Non-generic marker implemented by every single-value value object.

```csharp
public interface IValueObject
```

## Remarks

Reflection-driven integrations (OpenAPI schema transformers, EF Core conventions, model binder providers)
test for this interface because a single <code>IsAssignableTo</code> check is far cheaper than walking the
generic interface list of a type.

## Methods

### GetBoxedValue\(\) {#AdCodicem_ValueObjects_IValueObject_GetBoxedValue}

Gets the carried value, boxed.

```csharp
object? GetBoxedValue()
```

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

The underlying value.

#### Remarks

Reserved for reflection-driven code paths; prefer the strongly typed <code>Value</code> property.

