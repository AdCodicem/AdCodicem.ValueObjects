using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.AspNetCore;

// ValueObjectProblemDetails moved to AdCodicem.ValueObjects.AspNetCore.Http, which this package references, so that a
// minimal API names the member MVC writes without depending on MVC. The type keeps its namespace, and code compiled
// against this assembly finds it through the forward.
[assembly: TypeForwardedTo(typeof(ValueObjectProblemDetails))]
