using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;

namespace AdCodicem.ValueObjects.Wolverine.Http.Binding;

/// <summary>
/// Answers the refusals the reads recorded, before any middleware method or the handler runs.
/// </summary>
/// <param name="reads">
/// The reads of the endpoint, the policy's own list rather than a copy, which it declares as used when JasperFx writes the
/// code, so that every read is written before it, one a policy added after this frame was inserted included.
/// </param>
internal sealed class ValueObjectRefusalFrame(IReadOnlyList<ValueObjectReadFrame> reads) : AsyncFrame
{
    /// <inheritdoc />
    public override IEnumerable<Variable> FindVariables(IMethodVariables chain) => reads.Select(static read => read.Variable);

    /// <inheritdoc />
    public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
    {
        writer.WriteComment("Answer the value objects the request was refused for, with their rule codes");
        writer.Write(
            $"if (await global::AdCodicem.ValueObjects.Wolverine.Http.ValueObjectHttpBinding.TryAnswerRefusalsAsync(httpContext).ConfigureAwait(false)) {method.ToExitStatement()}");
        Next?.GenerateCode(method, writer);
    }
}
