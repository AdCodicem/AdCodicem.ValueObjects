# Namespace AdCodicem.ValueObjects.Testing.Data {#AdCodicem_ValueObjects_Testing_Data}

### Classes

 [ValueObjectSampler](AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.md)

Draws value objects their own rules accept, from the schema they declare: test data that never restates a rule the
type already holds.

 [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)

How a <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler" data-throw-if-not-resolved="false"></xref> draws: how many candidates it tries, how often it takes a known value of an open
set, how long a string without a maximum length gets, and the generators of the rules no schema carries.

 [ValueObjectSamplingException](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.md)

Thrown when a sampler cannot draw a value its type accepts: no candidate, drawn from its schema or from the generator
registered for it, passed its rules, and it declares no example they accept.

### Structs

 [SchemaViolation<TValue\>](AdCodicem.ValueObjects.Testing.Data.SchemaViolation\-1.md)

A value the schema of a value object rules out, with the rule it breaks.

