# AdCodicem.ValueObjects.AspNetCore.Http — minimal API problem details

Tests: `Web/` of `tests/AdCodicem.ValueObjects.UnitTests.AspNet` (reflection-based binding) and
`tests/AdCodicem.ValueObjects.RdgTests` (the Request Delegate Generator's binding). The filter runs natively: the
`native AOT` job guards it.

- A minimal API convention runs before the binder describes the parameters, so `WithValueObjectProblemDetails` reads
  them in its filter factory, from the `IParameterBindingMetadata` both binders add to the endpoint's metadata: the
  parameters it checks and the names they bind under come from there. `MethodInfo.GetParameters()` misses the members
  `[AsParameters]` expands and their names; it serves only to add no filter factory to a handler with no value object, no
  array of them and no `[AsParameters]` parameter.
- The filter mirrors each binder's empty-text rule, which differs: the reflection-based binding parses empty text; the RDG
  refuses no empty query text, binding `null` to a nullable value object and the default instance, unchecked, to one that
  cannot be `null`, and takes an empty header for an absent one. It tells the RDG's endpoints by the
  `GeneratedCodeAttribute` the RDG adds to their metadata, which an RdgTests test pins so that an SDK dropping it fails
  there first.
- The convention fails the build of an endpoint whose HTTP JSON options cannot serialize the problem details (no
  reflection and no `AddProblemDetails()`) rather than let each refusal end in a 500.
- `ValueObjectProblemDetails` and `ValueObjectMessageOptions` live here under the namespace
  `AdCodicem.ValueObjects.AspNetCore`, which the MVC package shares and which forwards `ValueObjectProblemDetails`
  (`TypeForwards.cs`): keep both the namespace and the forward. The Azure Functions ASP.NET Core package references
  them too.
- The refusal list and its validation problem are `src/Shared/ValueObjectRefusals.cs`, linked by the Azure Functions
  ASP.NET Core package too; the refusal of a request body, which only this package answers, is its `partial` part here
  (`Binding/ValueObjectRefusals.Body.cs`). Change the problem's shape there, once, for both.
- The messages a convention names belong to the endpoint's marker: a group's conventions run before the endpoint's own,
  so the last convention naming messages wins, and one without keeps them.
- The exception handler runs outside `UseRequestLocalization()` in the usual order, whose culture is gone by then: it
  applies the culture of `IRequestCultureFeature` again while it answers.
