using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.AspNetCore.Http;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The codes recorded on a request, called directly with what the binder never passes: no request, a member
/// recorded twice, and an entry of the request's items that something else overwrote.
/// </summary>
public class ProblemDetailsTests
{
    [Fact]
    public void Recording_a_code_without_a_request_does_nothing()
    {
        var record = () => ValueObjectProblemDetails.RecordErrorCode(null, "id", ValueObjectErrorCodes.NotParsable);

        record.Should().NotThrow();
        ValueObjectProblemDetails.GetErrorCodes(null).Should().BeEmpty();
    }

    [Fact]
    public void A_request_with_no_rejection_has_no_code()
        => ValueObjectProblemDetails.GetErrorCodes(new DefaultHttpContext()).Should().BeEmpty();

    [Fact]
    public void A_second_code_recorded_for_a_member_replaces_the_first()
    {
        var request = new DefaultHttpContext();

        ValueObjectProblemDetails.RecordErrorCode(request, "country", ValueObjectErrorCodes.TooLong);
        ValueObjectProblemDetails.RecordErrorCode(request, "country", ValueObjectErrorCodes.NotAKnownValue);

        ValueObjectProblemDetails.GetErrorCodes(request).Should().Equal(new Dictionary<string, string>
        {
            ["country"] = ValueObjectErrorCodes.NotAKnownValue,
        });
    }

    /// <summary>
    /// The items of a request are open to all its code. The codes are kept under a key no one else holds, and an
    /// entry someone overwrote anyway is not read as codes, and is replaced by the next one recorded.
    /// </summary>
    [Fact]
    public void Codes_overwritten_by_other_code_are_neither_read_nor_kept()
    {
        var request = new DefaultHttpContext();
        ValueObjectProblemDetails.RecordErrorCode(request, "country", ValueObjectErrorCodes.NotAKnownValue);
        foreach (var key in request.Items.Keys.ToList())
        {
            request.Items[key] = "overwritten";
        }

        var read = ValueObjectProblemDetails.GetErrorCodes(request);
        ValueObjectProblemDetails.RecordErrorCode(request, "email", ValueObjectErrorCodes.InvalidFormat);

        read.Should().BeEmpty();
        ValueObjectProblemDetails.GetErrorCodes(request).Should().Equal(new Dictionary<string, string>
        {
            ["email"] = ValueObjectErrorCodes.InvalidFormat,
        });
    }
}
