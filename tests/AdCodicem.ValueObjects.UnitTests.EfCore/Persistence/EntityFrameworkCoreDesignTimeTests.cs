using AdCodicem.ValueObjects.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What the converters do under a design-time tool, which <see cref="EF.IsDesignTime"/> says is running. The flag is
/// global, so these tests run alone.
/// </summary>
[Collection(nameof(EntityFrameworkCoreDesignTime))]
public sealed class EntityFrameworkCoreDesignTimeTests
{
    /// <summary>
    /// <c>dotnet ef dbcontext optimize</c> converts the default of every property, which Entity Framework Core takes
    /// for its sentinel, to write it into a compiled model: no column is written, so the converters hand the value over
    /// as it stands, where an application writing it is refused.
    /// </summary>
    [Fact]
    public void A_design_time_tool_converts_the_default_a_compiled_model_takes_for_its_sentinel()
    {
#pragma warning disable VO0010 // The default is the sentinel the design-time tool converts.
        var country = default(CountryCode);
        var customer = default(CustomerId);
#pragma warning restore VO0010
        var original = EF.IsDesignTime;

        try
        {
            EF.IsDesignTime = true;

            new ValueObjectConverter<CountryCode, string>().ConvertToProvider(country).Should().Be(string.Empty);
            new StrictValueObjectConverter<CustomerId, Guid>().ConvertToProvider(customer).Should().Be(Guid.Empty);
        }
        finally
        {
            EF.IsDesignTime = original;
        }

        new ValueObjectConverter<CountryCode, string>()
            .Invoking(converter => converter.ConvertToProvider(country))
            .Should().Throw<ValueObjectException>("the application is no design-time tool");
    }
}

/// <summary>Runs the tests that change <see cref="EF.IsDesignTime"/> alone, so that no other test sees it.</summary>
[CollectionDefinition(nameof(EntityFrameworkCoreDesignTime), DisableParallelization = true)]
public sealed class EntityFrameworkCoreDesignTime;
