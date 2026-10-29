# Namespace AdCodicem.ValueObjects.EntityFrameworkCore {#AdCodicem_ValueObjects_EntityFrameworkCore}

### Classes

 [NullableValueObjectComparer<TSelf\>](AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectComparer\-1.md)

Compares optional value objects using their own equality rather than the underlying value's, as
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectComparer%601" data-throw-if-not-resolved="false"></xref> compares required ones.

 [NullableValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter\-2.md)

Stores an optional value object over a value type as its bare underlying value, and a value it rejects as
<code>NULL</code>.

 [NullableValueObjectConverter<TSelf\>](AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter\-1.md)

Stores an optional value object over <xref href="System.String" data-throw-if-not-resolved="false"></xref> as its bare text, and a value it rejects as <code>NULL</code>.

 [StrictNullableValueObjectConverter<TSelf\>](AdCodicem.ValueObjects.EntityFrameworkCore.StrictNullableValueObjectConverter\-1.md)

Stores an optional value object over <xref href="System.String" data-throw-if-not-resolved="false"></xref> as its bare text, and a value it rejects as <code>NULL</code>,
and re-validates whatever comes back from the database.

 [StrictNullableValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.StrictNullableValueObjectConverter\-2.md)

Stores an optional value object over a value type as its bare underlying value, and a value it rejects as
<code>NULL</code>, and re-validates whatever comes back from the database.

 [StrictValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.StrictValueObjectConverter\-2.md)

Stores a value object as its bare underlying value, and re-validates whatever comes back from the database.

 [ValueObjectCheckConstraints](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckConstraints.md)

Reads the code of the rule a CHECK constraint written by <code>AddValueObjectCheckConstraints</code> refused a row under,
from the error the database provider throws.

 [ValueObjectCheckOptions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.md)

Chooses the rules <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.AddValueObjectCheckConstraints(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions%7d)" data-throw-if-not-resolved="false"></xref> writes as CHECK
constraints, and the dialect it writes a pattern in.

 [ValueObjectComparer<TSelf\>](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectComparer\-1.md)

Compares value objects using their own equality rather than the underlying value's.

 [ValueObjectConventionExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.md)

Maps value objects onto their underlying database types.

 [ValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConverter\-2.md)

Stores a value object as its bare underlying value.

 [ValueObjectDbContextOptionsBuilderExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectDbContextOptionsBuilderExtensions.md)

Adds to the options of a context what the model cannot hold: the translation of what a LINQ query reads of a value
object, and the code of the rule a CHECK constraint refused a row under.

 [ValueObjectInt16KeyGenerationExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt16KeyGenerationExtensions.md)

Generates a key over a 16-bit integer the way the provider generates a <xref href="System.Int16" data-throw-if-not-resolved="false"></xref> key.

 [ValueObjectInt32KeyGenerationExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt32KeyGenerationExtensions.md)

Generates a key over a 32-bit integer the way the provider generates an <xref href="System.Int32" data-throw-if-not-resolved="false"></xref> key.

 [ValueObjectInt64KeyGenerationExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt64KeyGenerationExtensions.md)

Generates a key over a 64-bit integer the way the provider generates a <xref href="System.Int64" data-throw-if-not-resolved="false"></xref> key.

 [ValueObjectKeyGeneratorFactory<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectKeyGeneratorFactory\-2.md)

Creates the generator <code>HasValueObjectKeyGeneration</code> gives a key, by a type a compiled model can instantiate:
GUIDs a database keeps in order for a key over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, and the temporary values Entity Framework Core
holds until the database generates a key over an integer.

### Enums

 [ValueObjectChecks](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectChecks.md)

The rules of a value object that <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.AddValueObjectCheckConstraints(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions%7d)" data-throw-if-not-resolved="false"></xref> writes as
CHECK constraints, combined as flags.

 [ValueObjectPatternDialect](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectPatternDialect.md)

The regular expressions of the database a pattern is written for as a CHECK constraint, which the model cannot tell.

