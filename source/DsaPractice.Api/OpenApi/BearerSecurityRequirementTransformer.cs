using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DsaPractice.Api.OpenApi;

/// <summary>
/// Marks the operations that actually require a token, so the document distinguishes "send a
/// bearer token here" from "this endpoint is public" instead of declaring the scheme globally and
/// leaving every reader to guess which is which. Driven by the endpoint's own authorization
/// metadata, so it cannot drift from what the pipeline enforces.
/// </summary>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = []
            }
        ];

        operation.Responses ??= new OpenApiResponses();
        operation.Responses["401"] = new OpenApiResponse { Description = "No token, or a token this Api does not accept." };

        return Task.CompletedTask;
    }
}
