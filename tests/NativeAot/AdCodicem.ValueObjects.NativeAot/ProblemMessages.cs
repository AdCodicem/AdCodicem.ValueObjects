namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The type the messages of <c>ProblemMessages.resx</c> are named after, which the <c>/localized</c> group of
/// <see cref="Problems"/> answers its refusals with.
/// </summary>
/// <remarks>
/// The resource is neutral: the application runs with <c>InvariantGlobalization</c>, under the invariant culture, which
/// reads the neutral resource. Its messages are worded apart from the rules' English ones, so that a run that looks
/// nothing up answers otherwise than one that does.
/// </remarks>
internal sealed class ProblemMessages;
