using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DsaPractice.Api.OpenApi;

/// <summary>
/// Declares the bearer scheme in the OpenAPI document, so the generated client types and the
/// Scalar reference page both know the Api expects a token.
///
/// Without it Scalar renders an "Authorize" button for nothing and every call from that page is
/// anonymous -- which, now that submissions require a signed-in caller, makes the one tool for
/// trying the Api by hand unable to reach half of it.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "An access token from the identity provider, or one minted by `dotnet user-jwts create` locally."
        };

        return Task.CompletedTask;
    }
}
