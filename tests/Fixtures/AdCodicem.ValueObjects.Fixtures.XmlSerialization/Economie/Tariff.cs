// The namespace is \u00C9conomie, spelt with an escape C# reads in an identifier, so that the file stays ASCII.
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.XmlSerialization.\u00C9conomie;

/// <summary>A tariff, in a CLR namespace a URI escapes.</summary>
[ValueObject<string>(MaxLength = 8)]
public readonly partial struct Tariff;

/// <summary>A class of the same CLR namespace, whose data contract namespace <c>DataContractSerializer</c> derives.</summary>
public sealed class Invoice;
