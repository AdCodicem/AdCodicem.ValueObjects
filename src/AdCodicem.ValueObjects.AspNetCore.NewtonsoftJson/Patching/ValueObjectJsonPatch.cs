using Microsoft.AspNetCore.JsonPatch;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// Applies a JSON Patch document whose values each value object reads through its rules.
/// </summary>
internal static class ValueObjectJsonPatch
{
    /// <summary>
    /// Applies the operations, handing each error the location it concerns and the code of the rule a value broke.
    /// </summary>
    /// <remarks>
    /// The location is the operation's <c>path</c>, as the client sent it, or its <c>from</c> for a <c>copy</c> or a
    /// <c>move</c> refused at its source, a <c>from</c> JSON Patch cannot read included, and the empty string for an
    /// operation that names no <c>path</c>. Every operation is applied in turn, after one that failed too; JSON Patch
    /// stops at an operation it cannot read, an unknown <c>op</c> or a path with a bad escape, which is reported.
    /// </remarks>
    /// <typeparam name="TModel">The type the document patches.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="target">The object it patches.</param>
    /// <param name="report">Called with each error, its location, and the code of the rule a value broke or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every operation applied.</returns>
    public static bool Apply<TModel>(JsonPatchDocument<TModel> document, TModel target, Action<JsonPatchError, string, string?> report)
        where TModel : class
    {
        var conversion = new ValueObjectPatchConversion(document.ContractResolver);
        var factory = new ValueObjectAdapterFactory(conversion);
        var applied = true;

        void Report(JsonPatchError error, string? location)
        {
            applied = false;
            report(error, location ?? string.Empty, conversion.TakeCode());
        }

        var adapter = new ValueObjectObjectAdapter(document.ContractResolver, factory, conversion, Report);
        document.ApplyTo(target, adapter, adapter.Log);

        return applied;
    }
}
