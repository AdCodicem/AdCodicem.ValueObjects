using AdCodicem.ValueObjects.UnitTests.Domain;
using MessagePack;

// MessagePack's analyzer reaches this project through the package, as it reaches an application, and fails the build of
// a [MessagePackObject] type holding a type of the same assembly it knows no formatter for (MsgPack003). A value object is
// written by the resolver the options hold, which no analyzer sees, so each one a [MessagePackObject] type of the suite
// holds is assumed formattable, as the guide tells an application to.
[assembly: MessagePackAssumedFormattable(typeof(CustomerId))]
[assembly: MessagePackAssumedFormattable(typeof(Iban))]
[assembly: MessagePackAssumedFormattable(typeof(Quantity))]
[assembly: MessagePackAssumedFormattable(typeof(CountryCode))]
