# AdCodicem.ValueObjects.Dapper

- Reads go through `CreateUnchecked`, the application having validated what it wrote, except a column the value object
  cannot have written (`ValueObjectTypeHandler`): a `string` read into a value object over another type goes through
  `TryParse`, a number or a `Guid` read into one over `string` through `TryCreate`; `NULL` is refused
  `value_object.required`. A refusal is a `DataException`.
- `AddValueObjectHandlers` registers what the registry lists; each construction of a generic value object needs
  `AddValueObjectHandler<TSelf, TValue>()`.
- `Schema.IsUnicode` (`Unicode = false`) makes the handler send `varchar` parameters, as EF Core does for the column; the
  integration suite holds both to SQL Server.
