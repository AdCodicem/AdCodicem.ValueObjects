using Marten;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Teaches the document store <c>AddMarten</c> registers the value objects its documents hold.
/// </summary>
public static class ValueObjectMartenConfigurationExtensions
{
    /// <summary>
    /// Applies <see cref="ValueObjectStoreOptionsExtensions.UseValueObjects(StoreOptions)"/> after <c>AddMarten</c>'s own
    /// configuration, its lambda or its <see cref="StoreOptions"/>, whatever the order of the calls inside it.
    /// </summary>
    /// <remarks>
    /// It registers through <c>ConfigureMarten</c>, which Marten applies in the order of registration: a
    /// <c>ConfigureMarten</c> registered after it runs later, and one that replaces the serializer there
    /// (<c>UseSystemTextJsonForSerialization</c>) drops the converter factory. Register such a configuration before this
    /// call, or call <see cref="ValueObjectStoreOptionsExtensions.UseValueObjects(StoreOptions)"/> again at its end.
    /// </remarks>
    /// <param name="marten">What <c>AddMarten</c> returned.</param>
    /// <returns>The same expression.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="marten"/> is <see langword="null"/>.</exception>
    public static MartenServiceCollectionExtensions.MartenConfigurationExpression UseValueObjects(
        this MartenServiceCollectionExtensions.MartenConfigurationExpression marten)
    {
        ArgumentNullException.ThrowIfNull(marten);

        // Marten applies what ConfigureMarten registers after AddMarten's own configuration, in the order of registration,
        // before the store is built.
        marten.Services.ConfigureMarten(static options => options.UseValueObjects());
        return marten;
    }
}
