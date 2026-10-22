using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests;

/// <remarks>
/// Every project of the unit suite compiles this file (tests/UnitTests.props): a project's registry holds the value
/// objects its own module initializer registers, so each project sweeps those its family's tests declare, as the one
/// assembly swept them all before the suite was split by family.
/// </remarks>
public sealed partial class DescriptorTests
{
    [Fact]
    public void Every_rejection_carries_an_error_code()
    {
        // Consumers turn these codes into ProblemDetails, so a rejection without one surfaces to an API client
        // as a null. Sweep every registered type rather than trusting the handful spelled out beside this test.
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            descriptor.TryParse(" an unlikely value ", CultureInfo.InvariantCulture, out _, out var validation);

            if (!validation.IsValid)
            {
                validation.ErrorCode.Should().NotBeNullOrEmpty(
                    "'{0}' must say why it rejected a value",
                    descriptor.ValueObjectType.Name);
            }
        }
    }
}
