# AdCodicem.ValueObjects.AzureFunctions.AspNetCore — the problem details middleware

The middleware answers a refused HTTP trigger of the ASP.NET Core integration with the minimal API's validation problem.
Not AOT-compatible.

- Its answer reaches the client only from inside the integration's `FunctionsHttpProxyingMiddleware`, which sets the
  request on the invocation (`GetHttpContext()`) before it calls the middleware: the middleware reads it when it starts,
  and without it rethrows and warns once rather than swallow the failure into an empty 200, a refused body included,
  whose codes MVC records on the request the integration sets.
- The invocation runs on the host's gRPC call, never in the request's culture: the middleware applies the culture of
  `IRequestCultureFeature`, or else runs the framework's `RequestLocalizationMiddleware` around the lookup
  (`UseValueObjectProblemDetails(messages => …)`).
- `GetInvocationResult()` reads the worker's internal `IFunctionBindingsFeature`; `Functions/FunctionsDoubles.cs` of
  `tests/AdCodicem.ValueObjects.UnitTests.Hosts` substitutes it, found by name, NSubstitute implementing it because
  Worker.Core grants `DynamicProxyGenAssembly2`.
- `Microsoft.Azure.Functions.Worker` is pinned at 2.52.0 beside Worker.Core, which makes it a dependency of this package,
  listed in `.github/shipped-dependencies`: Extensions.Http.AspNetCore asks for Worker 2.1.0, whose Worker.Grpc would run
  beside Worker.Core 2.52.0.
