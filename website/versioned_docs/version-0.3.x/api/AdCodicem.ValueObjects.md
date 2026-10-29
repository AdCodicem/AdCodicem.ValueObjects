# Namespace AdCodicem.ValueObjects {#AdCodicem_ValueObjects}

### Namespaces

 [AdCodicem.ValueObjects.AI](AdCodicem.ValueObjects.AI.md)

 [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)

 [AdCodicem.ValueObjects.AspNetCore](AdCodicem.ValueObjects.AspNetCore.md)

 [AdCodicem.ValueObjects.AutoFixture](AdCodicem.ValueObjects.AutoFixture.md)

 [AdCodicem.ValueObjects.AzureFunctions](AdCodicem.ValueObjects.AzureFunctions.md)

 [AdCodicem.ValueObjects.Blazor](AdCodicem.ValueObjects.Blazor.md)

 [AdCodicem.ValueObjects.Bogus](AdCodicem.ValueObjects.Bogus.md)

 [AdCodicem.ValueObjects.Dapper](AdCodicem.ValueObjects.Dapper.md)

 [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)

 [AdCodicem.ValueObjects.FluentValidation](AdCodicem.ValueObjects.FluentValidation.md)

 [AdCodicem.ValueObjects.FsCheck](AdCodicem.ValueObjects.FsCheck.md)

 [AdCodicem.ValueObjects.Grpc](AdCodicem.ValueObjects.Grpc.md)

 [AdCodicem.ValueObjects.HotChocolate](AdCodicem.ValueObjects.HotChocolate.md)

 [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)

 [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)

 [AdCodicem.ValueObjects.MessagePack](AdCodicem.ValueObjects.MessagePack.md)

 [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)

 [AdCodicem.ValueObjects.ModelContextProtocol](AdCodicem.ValueObjects.ModelContextProtocol.md)

 [AdCodicem.ValueObjects.MongoDB](AdCodicem.ValueObjects.MongoDB.md)

 [AdCodicem.ValueObjects.NJsonSchema](AdCodicem.ValueObjects.NJsonSchema.md)

 [AdCodicem.ValueObjects.NewtonsoftJson](AdCodicem.ValueObjects.NewtonsoftJson.md)

 [AdCodicem.ValueObjects.OpenApi](AdCodicem.ValueObjects.OpenApi.md)

 [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)

 [AdCodicem.ValueObjects.Serilog](AdCodicem.ValueObjects.Serilog.md)

 [AdCodicem.ValueObjects.Swashbuckle](AdCodicem.ValueObjects.Swashbuckle.md)

### Classes

 [UnderlyingValue](AdCodicem.ValueObjects.UnderlyingValue.md)

Thin generic bridges to the underlying type's own parsing and formatting, used by generated code.

 [ValueObjectBound](AdCodicem.ValueObjects.ValueObjectBound.md)

A generic bridge to the bounds a value object declares through <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>, used by generated code.

 [ValueObjectErrorCodes](AdCodicem.ValueObjects.ValueObjectErrorCodes.md)

Well-known validation error codes shared by the framework and its integrations.

 [ValueObjectErrors](AdCodicem.ValueObjects.ValueObjectErrors.md)

Carries the code of a violated rule across an exception that a serializer, a data access library or any other
integration throws in its own terms.

 [ValueObjectExample](AdCodicem.ValueObjects.ValueObjectExample.md)

A generic bridge to the example a value object declares through <xref href="AdCodicem.ValueObjects.IValueObjectExample%601" data-throw-if-not-resolved="false"></xref>, used by
generated code.

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

Thrown when a value object is constructed from a value that violates one of its rules.

 [ValueObjectJsonException](AdCodicem.ValueObjects.ValueObjectJsonException.md)

A value object refused the value System.Text.Json was reading or writing.

 [ValueObjectPattern](AdCodicem.ValueObjects.ValueObjectPattern.md)

A generic bridge to the pattern a value object declares through <xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref>,
used by generated code.

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

 [IValueObjectExample<TSelf\>](AdCodicem.ValueObjects.IValueObjectExample\-1.md)

Declares the example of a value object, which its OpenAPI and JSON schemas publish.

 [IValueObjectFormatter<TValue\>](AdCodicem.ValueObjects.IValueObjectFormatter\-1.md)

Declares that a value object formats itself, rather than deferring to its underlying value.

 [IValueObjectMaximum<TValue\>](AdCodicem.ValueObjects.IValueObjectMaximum\-1.md)

Declares the inclusive upper bound of a value object, as a value of its underlying type.

 [IValueObjectMinimum<TValue\>](AdCodicem.ValueObjects.IValueObjectMinimum\-1.md)

Declares the inclusive lower bound of a value object, as a value of its underlying type.

 [IValueObjectNormalizer<TValue\>](AdCodicem.ValueObjects.IValueObjectNormalizer\-1.md)

Declares that a value object normalizes its underlying value before validating it.

 [IValueObjectPatternValidator](AdCodicem.ValueObjects.IValueObjectPatternValidator.md)

Declares that a string value object must match a regular expression.

 [IValueObjectSpanNormalizer](AdCodicem.ValueObjects.IValueObjectSpanNormalizer.md)

Declares that a string value object can normalize straight from text, without materializing it first.

 [IValueObjectStringFormatter<TValue\>](AdCodicem.ValueObjects.IValueObjectStringFormatter\-1.md)

Declares that a value object produces its text directly, when writing into a buffer would be wasteful.

 [IValueObjectValidator<TValue\>](AdCodicem.ValueObjects.IValueObjectValidator\-1.md)

Declares that a value object enforces a rule its declarative constraints cannot express.

