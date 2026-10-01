using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

// The contract kit over one value object per underlying type and per hook the rest of the domain leaves out
// (Domain/UnderlyingTypes.cs), so that what the generator emits for each of them runs rather than only compiles.

/// <inheritdoc cref="IbanContract" />
public sealed class LatitudeContract : ValueObjectContract<Latitude, double>
{
    protected override IEnumerable<double> AcceptedValues => [-90d, 0d, 48.8566d, 90d];

    protected override IEnumerable<double> RejectedValues =>
        [-90.0001d, 90.0001d, double.NegativeInfinity, double.PositiveInfinity, double.NaN];
}

/// <inheritdoc cref="IbanContract" />
public sealed class RatioContract : ValueObjectContract<Ratio, float>
{
    protected override IEnumerable<float> AcceptedValues => [0f, 0.5f, 1f];

    protected override IEnumerable<float> RejectedValues => [-0.1f, 1.1f, float.NaN];
}
