namespace DsaPractice.Api.Endpoints;

// A named type rather than an anonymous object so /health has a schema in the OpenAPI document
// like every other endpoint.
internal sealed record HealthResponse(string Status);
