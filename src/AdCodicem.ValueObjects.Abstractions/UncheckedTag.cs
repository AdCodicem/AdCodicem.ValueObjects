using System.ComponentModel;

namespace AdCodicem.ValueObjects;

/// <summary>
/// The second parameter of the private constructor a generated value object stores its value through, which only the
/// generated code supplies.
/// </summary>
/// <remarks>
/// <para>
/// The constructor stores the value as it is, so every public way in goes through it after normalizing and validating
/// (<c>Create</c>, <c>TryCreate</c>, <c>Parse</c>, <c>TryParse</c>), or with the caller's word that the value is valid
/// (<c>CreateUnchecked</c>). A reflection-driven object mapper, AutoMapper among them, constructs a type through any
/// constructor taking the source value alone, private ones included: with one argument, it would wrap a value no rule
/// has checked, and the instance would look as valid as any other. Nothing outside the generated code has a reason to
/// supply this type, so a mapper finds no constructor it can call and fails loudly instead.
/// </para>
/// <para>
/// The parameter is required: a mapper may fill an optional parameter from its default value.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct UncheckedTag;
