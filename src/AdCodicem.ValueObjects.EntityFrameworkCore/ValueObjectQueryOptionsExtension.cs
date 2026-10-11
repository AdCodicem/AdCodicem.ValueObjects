using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Registers the translator of <c>.Value</c> with the services of a context, as
/// <see cref="ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectQueries(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)"/>
/// asks.
/// </summary>
/// <remarks>
/// <para>
/// Query services cannot be added from <c>ConfigureConventions</c>, which shapes the model: they are services of the
/// context, which an extension of its options adds.
/// </para>
/// <para>
/// Entity Framework Core adds them only to a service provider it builds itself, never to the one
/// <c>UseInternalServiceProvider</c> names, where <c>.Value</c> would stay untranslated without a word: the extension
/// refuses that provider, unless it holds them.
/// </para>
/// </remarks>
internal sealed class ValueObjectQueryOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    /// <inheritdoc />
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public void ApplyServices(IServiceCollection services)
    {
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMemberTranslatorPlugin, ValueObjectMemberTranslatorPlugin>();
        services.TryAddSingleton<Applied>();
    }

    /// <summary>
    /// Refuses a service provider of the application's own, named by <c>UseInternalServiceProvider</c>, that does not
    /// hold the services of the extension.
    /// </summary>
    /// <param name="options">The options of the context.</param>
    /// <exception cref="InvalidOperationException">
    /// The context runs on a service provider of the application's own, to which the services of the extension were
    /// not added.
    /// </exception>
    /// <remarks>
    /// Entity Framework Core validates the options each time a context starts on such a provider, before it initializes
    /// the provider's options: the check asks the provider for the mark the extension adds, and builds nothing else.
    /// </remarks>
    public void Validate(IDbContextOptions options)
    {
        if (options.FindExtension<CoreOptionsExtension>()?.InternalServiceProvider is { } provider
            && provider.GetService(typeof(Applied)) is null)
        {
            throw new InvalidOperationException(
                "UseValueObjectQueries() cannot translate .Value on this context: it runs on the service provider "
                + "UseInternalServiceProvider names, to which Entity Framework Core adds no service of an options "
                + "extension. Build the context without UseInternalServiceProvider, or add to that provider the services "
                + "of the extensions of its options, through IDbContextOptionsExtension.ApplyServices, as Entity "
                + "Framework Core does to a provider it builds.");
        }
    }

    /// <summary>
    /// Marks a service provider that holds the services of the extension.
    /// </summary>
    private sealed class Applied;

    /// <summary>
    /// Describes the extension to Entity Framework Core, which keys its cache of service providers on it.
    /// </summary>
    /// <param name="extension">The extension described.</param>
    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        /// <inheritdoc />
        public override bool IsDatabaseProvider => false;

        /// <inheritdoc />
        public override string LogFragment => "using value object queries ";

        /// <inheritdoc />
        public override int GetServiceProviderHashCode() => 0;

        /// <inheritdoc />
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        /// <inheritdoc />
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["AdCodicem.ValueObjects:" + nameof(ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectQueries)] = "1";
    }
}
