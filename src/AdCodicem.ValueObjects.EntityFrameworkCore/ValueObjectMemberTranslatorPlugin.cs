using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Hands Entity Framework Core the translator of <c>.Value</c>, ahead of its own member translators.
/// </summary>
/// <param name="typeMappingSource">Source of the type mapping a translated column takes.</param>
/// <param name="sqlExpressionFactory">Factory of the conversion an expression other than a column takes.</param>
internal sealed class ValueObjectMemberTranslatorPlugin(
    IRelationalTypeMappingSource typeMappingSource,
    ISqlExpressionFactory sqlExpressionFactory) : IMemberTranslatorPlugin
{
    /// <inheritdoc />
    public IEnumerable<IMemberTranslator> Translators { get; } =
        [new ValueObjectMemberTranslator(typeMappingSource, sqlExpressionFactory)];
}
