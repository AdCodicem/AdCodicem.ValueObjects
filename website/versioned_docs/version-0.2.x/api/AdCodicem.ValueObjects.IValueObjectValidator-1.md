# Interface IValueObjectValidator<TValue\> {#AdCodicem_ValueObjects_IValueObjectValidator_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares that a value object enforces a rule its declarative constraints cannot express.

```csharp
public interface IValueObjectValidator<TValue>
```

#### Type Parameters

`TValue` 

Underlying value type.

## Remarks

Length, pattern and bounds are better declared on <code>[ValueObject&lt;T&gt;]</code>, where they also size the
database column and describe the OpenAPI schema. Implement this for what is left: an IBAN's MOD-97 check
digits, a Luhn checksum, a rule spanning several characters. The declared constraints run first, so this
method only sees values that already satisfy them.

## Methods

### ValidateValue\(in TValue\) {#AdCodicem_ValueObjects_IValueObjectValidator_1_ValidateValue__0__}

Decides whether a normalized value is acceptable.

```csharp
public static abstract ValidationResult ValidateValue(in TValue value)
```

#### Parameters

`value` TValue

Normalized value to check.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

Success, or the rule that rejected the value.

