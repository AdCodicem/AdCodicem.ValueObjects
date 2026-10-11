namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The type the French messages of <c>ValidationMessages.fr.resx</c> are named after, as an application names its
/// resources: the example of the errors reference, word for word.
/// </summary>
/// <remarks>
/// The resource holds French alone, so that a request in another culture finds no message and keeps the English one.
/// Its namespace is the assembly's root namespace followed by the folder, the name the build gives the resource, or every
/// key would be reported missing.
/// </remarks>
public sealed class ValidationMessages;
