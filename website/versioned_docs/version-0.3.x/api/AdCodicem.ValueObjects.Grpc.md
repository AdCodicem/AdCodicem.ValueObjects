# Namespace AdCodicem.ValueObjects.Grpc {#AdCodicem_ValueObjects_Grpc}

### Classes

 [ValueObjectBadRequest](AdCodicem.ValueObjects.Grpc.ValueObjectBadRequest.md)

Creates the value objects a contract-first request maps its fields to, records each value they refuse as a
<code>google.rpc.BadRequest</code> field violation, and answers them all at once as <code>INVALID_ARGUMENT</code>.

 [ValueObjectExceptionInterceptor](AdCodicem.ValueObjects.Grpc.ValueObjectExceptionInterceptor.md)

Answers a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> that escapes a gRPC service method with <code>INVALID_ARGUMENT</code> and a
<code>google.rpc.ErrorInfo</code> carrying the rule's code, rather than <code>UNKNOWN</code>.

