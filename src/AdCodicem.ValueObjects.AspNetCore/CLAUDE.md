# AdCodicem.ValueObjects.AspNetCore — MVC

Not AOT-compatible: MVC never runs in a native binary.

- MVC does not narrow its value providers to a greedy binding source. `AddValueObjects` inserts
  `ValueObjectModelBinderProvider` first, ahead of MVC's providers for a header, the body, the services and a binder
  `[ModelBinder(typeof(…))]` names, so it must decline a greedy source **before** its cache, which is keyed by type alone;
  claiming one bound a `[FromHeader]` or `[FromBody]` value object from a route, query or form value of its name, or left
  it at its default instance.
- MVC's header binder then asks for a binder again under `BindingSource.ModelBinding`, which is this package's, with a
  value provider holding the header alone, so a refused header records its code as a query value does, under MVC's key:
  the name the attribute gives, else the member's, prefixed as MVC prefixes a model's members; for a parameter naming no
  header, the empty key, unless a route, query or form value carries the parameter's name.
- `TypeForwards.cs` forwards `ValueObjectProblemDetails` to `AdCodicem.ValueObjects.AspNetCore.Http`, under the shared
  namespace `AdCodicem.ValueObjects.AspNetCore`: keep both, or code compiled against an earlier MVC package breaks.
- Messages: `AddValueObjects(messages => messages.UseResource<T>())` on `MvcOptions` or the builder localises the binder's
  and the JSON input formatter's refusals (`ValueObjectLocalizedMessages`, `src/Shared/CLAUDE.md`).
