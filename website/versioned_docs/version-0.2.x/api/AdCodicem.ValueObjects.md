# Namespace AdCodicem.ValueObjects {#AdCodicem_ValueObjects}

### Namespaces

 [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)

 [AdCodicem.ValueObjects.AspNetCore](AdCodicem.ValueObjects.AspNetCore.md)

 [AdCodicem.ValueObjects.Dapper](AdCodicem.ValueObjects.Dapper.md)

 [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)

 [AdCodicem.ValueObjects.FluentValidation](AdCodicem.ValueObjects.FluentValidation.md)

 [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)

 [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)

 [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)

 [AdCodicem.ValueObjects.NewtonsoftJson](AdCodicem.ValueObjects.NewtonsoftJson.md)

 [AdCodicem.ValueObjects.OpenApi](AdCodicem.ValueObjects.OpenApi.md)

### Classes

 [UnderlyingValue](AdCodicem.ValueObjects.UnderlyingValue.md)

Thin generic bridges to the underlying type's own parsing and formatting, used by generated code.

 [ValueObjectErrorCodes](AdCodicem.ValueObjects.ValueObjectErrorCodes.md)

Well-known validation error codes shared by the framework and its integrations.

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

Thrown when a value object is constructed from a value that violates one of its rules.

### Structs

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

Outcome of validating a candidate underlying value for a value object.

### Interfaces

 [INumericValueObject<TSelf, TValue\>](AdCodicem.ValueObjects.INumericValueObject\-2.md)

A value object over a numeric underlying type, exposing arithmetic through the generic math interfaces.

 [IValueObject<TValue\>](AdCodicem.ValueObjects.IValueObject\-1.md)

A value object carrying a single value of type <code class="typeparamref">TValue</code>.

 [IValueObject<TSelf, TValue\>](AdCodicem.ValueObjects.IValueObject\-2.md)

The full contract of a single-value value object, self-referencing so that construction, parsing and
comparison are resolved statically without reflection or boxing.

 [IValueObject](AdCodicem.ValueObjects.IValueObject.md)

Non-generic marker implemented by every single-value value object.

 [IValueObjectFormatter<TValue\>](AdCodicem.ValueObjects.IValueObjectFormatter\-1.md)

Declares that a value object formats itself, rather than deferring to its underlying value.

 [IValueObjectNormalizer<TValue\>](AdCodicem.ValueObjects.IValueObjectNormalizer\-1.md)

Declares that a value object normalizes its underlying value before validating it.

 [IValueObjectSpanNormalizer](AdCodicem.ValueObjects.IValueObjectSpanNormalizer.md)

Declares that a string value object can normalize straight from text, without materializing it first.

 [IValueObjectStringFormatter<TValue\>](AdCodicem.ValueObjects.IValueObjectStringFormatter\-1.md)

Declares that a value object produces its text directly, when writing into a buffer would be wasteful.

 [IValueObjectValidator<TValue\>](AdCodicem.ValueObjects.IValueObjectValidator\-1.md)

Declares that a value object enforces a rule its declarative constraints cannot express.

