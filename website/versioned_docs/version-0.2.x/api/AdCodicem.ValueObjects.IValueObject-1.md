# Interface IValueObject<TValue\> {#AdCodicem_ValueObjects_IValueObject_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A value object carrying a single value of type <code class="typeparamref">TValue</code>.

```csharp
public interface IValueObject<TValue> : IValueObject
```

#### Type Parameters

`TValue` 

Underlying value type.

#### Implements

[IValueObject](AdCodicem.ValueObjects.IValueObject.md)

## Properties

### Value {#AdCodicem_ValueObjects_IValueObject_1_Value}

Gets the carried value. Always normalized and always valid, unless the instance is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a>.

```csharp
TValue Value { get; }
```

#### Property Value

 TValue

