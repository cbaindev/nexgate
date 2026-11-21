using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Nexgate.Api.Rest.Authorization;

namespace Nexgate.Api.Rest.Extensions.OpenApi;

/// <summary>
/// Descripción OpenAPI de la API v1 (contrato del frontend, D-34) y Swagger UI solo en <c>Development</c>
/// (design de add-openapi-contract).
/// </summary>
public static class OpenApiSetup
{
    public const string DocumentName = "v1";
    public const string DocumentPath = "/openapi/v1.json";
    public const string SwaggerUiPrefix = "swagger";
    public const string BearerScheme = "Bearer";

    private const string AccessTokenKey = "nexgate:access-token";

    /// <summary>Etiquetas del documento, con su descripción en español. Cada endpoint usa una.</summary>
    public static readonly IReadOnlyDictionary<string, string> Tags = new Dictionary<string, string>
    {
        ["Auth"] = "Inicio de sesión, renovación de tokens y cierre de sesión.",
        ["Me"] = "Usuario actual, sus organizaciones y sus permisos efectivos.",
        ["Invitations"] = "Invitaciones a la organización activa y su aceptación.",
        ["Members"] = "Miembros de la organización activa y sus roles por alcance.",
        ["Organization"] = "Organización activa.",
        ["Sites"] = "Sitios de la organización activa.",
        ["Platform"] = "Operaciones del operador de plataforma."
    };

    private static readonly IReadOnlyDictionary<string, string> ResponseDescriptions = new Dictionary<string, string>
    {
        ["200"] = "Operación correcta.",
        ["201"] = "Recurso creado.",
        ["204"] = "Operación correcta, sin contenido.",
        ["400"] = "Solicitud inválida: el detalle de cada campo viene en errors.",
        ["401"] = "Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas.",
        ["403"] = "Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied).",
        ["404"] = "El recurso no existe o no es visible para el usuario.",
        ["409"] = "Conflicto con el estado actual; el código indica cuál.",
        ["429"] = "Demasiados intentos; reintentar más tarde (too_many_requests).",
        ["500"] = "Error inesperado; el identificador de correlación sirve para buscarlo en los logs."
    };

    public static IServiceCollection AddNexgateOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            // Solo los endpoints de negocio, que viven bajo el prefijo de versión (salud queda afuera).
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/v1/", StringComparison.Ordinal) == true;
            options.AddSchemaTransformer(ProblemSchemas);
            options.AddSchemaTransformer(ConstructorParameterLengths);
            options.AddOperationTransformer(MarkAccessToken);
            options.AddOperationTransformer(JsonRequestBody);
            options.AddDocumentTransformer(Finish);
        });

    /// <summary>Documento en <see cref="DocumentPath"/> y Swagger UI en <c>/swagger</c>, solo en <c>Development</c>.</summary>
    public static WebApplication MapNexgateApiReference(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi(DocumentPath);
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(DocumentPath, "Nexgate API v1");
            options.RoutePrefix = SwaggerUiPrefix;
            options.DocumentTitle = "Nexgate API";
            // Sin EnablePersistAuthorization: el token no sobrevive a una recarga.
            options.DisplayOperationId();
        });
        return app;
    }

    /// <summary>
    /// Los errores llevan siempre <c>code</c> y <c>correlationId</c> (platform-api, <c>Standard error format</c>).
    /// </summary>
    private static Task ProblemSchemas(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        if (!context.JsonTypeInfo.Type.IsAssignableTo(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["code"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Código de error estable, para que el cliente decida qué hacer (por ejemplo, permission_denied)."
        };
        schema.Properties["correlationId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identificador de correlación de la solicitud; también viene en el encabezado X-Correlation-Id."
        };
        schema.Required ??= new HashSet<string>();
        schema.Required.Add("title");
        schema.Required.Add("status");
        schema.Required.Add("code");
        schema.Required.Add("correlationId");
        return Task.CompletedTask;
    }

    /// <summary>
    /// En los modelos de solicitud (records), los controllers de ASP.NET Core exigen los atributos de validación sobre los parámetros del constructor,
    /// pero el generador del documento solo los lee de las propiedades. Se copian los límites de longitud del parámetro
    /// asociado a cada propiedad, para que el contrato los declare igual.
    /// </summary>
    private static Task ConstructorParameterLengths(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        if (schema.Properties is not { Count: > 0 } properties)
        {
            return Task.CompletedTask;
        }

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            var length = property.AssociatedParameter?.AttributeProvider?
                .GetCustomAttributes(typeof(StringLengthAttribute), inherit: false)
                .OfType<StringLengthAttribute>()
                .FirstOrDefault();
            if (length is not null && properties.TryGetValue(property.Name, out var target) && target is OpenApiSchema concrete)
            {
                concrete.MaxLength = length.MaximumLength;
                concrete.MinLength = length.MinimumLength;
            }
        }

        return Task.CompletedTask;
    }

    private static Task MarkAccessToken(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken _)
    {
        // El requisito de acceso de la acción (o de su controller) indica si acepta el token y si es obligatorio.
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<AccessRequirementAttribute>().LastOrDefault() is
            { AcceptsToken: true } access)
        {
            operation.Metadata ??= new Dictionary<string, object>();
            operation.Metadata[AccessTokenKey] = access.RequiresToken;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// El contrato declara los cuerpos de entrada solo como <c>application/json</c>, que es lo que envían los clientes
    /// generados. El formateador de entrada de los controllers además acepta <c>text/json</c> y <c>application/*+json</c>; no se anuncian para
    /// que el contrato no dependa del formateador.
    /// </summary>
    private static Task JsonRequestBody(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken _)
    {
        if (operation.RequestBody?.Content is { } content)
        {
            foreach (var mediaType in content.Keys.Where(k => k != "application/json").ToList())
            {
                content.Remove(mediaType);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Datos generales, esquema de seguridad, etiquetas y orden determinista: dos generaciones del mismo código dan el
    /// mismo documento. Sin <c>servers</c>: la URL base la fija el cliente o el proxy.
    /// </summary>
    private static Task Finish(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken _)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Nexgate API",
            Version = DocumentName,
            Description = "API de Nexgate, plataforma multitenant de control de acceso físico. Este documento es el contrato " +
                "del que se generan los clientes. Toda operación de negocio actúa sobre la organización activa del token."
        };
        document.Servers = [];

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token de acceso de POST /api/v1/auth/token."
            }
        };

        var sortedPaths = new OpenApiPaths();
        foreach (var (path, item) in (document.Paths ?? []).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var operation in item.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                ApplySecurity(document, operation);
                if (operation.Responses is { } responses)
                {
                    var sorted = new OpenApiResponses();
                    foreach (var response in responses.OrderBy(r => r.Key, StringComparer.Ordinal))
                    {
                        if (response.Value is OpenApiResponse concrete && ResponseDescriptions.TryGetValue(response.Key, out var text))
                        {
                            concrete.Description = text;
                        }

                        sorted[response.Key] = response.Value;
                    }

                    operation.Responses = sorted;
                }
            }

            sortedPaths[path] = item;
        }

        document.Paths = sortedPaths;

        if (document.Components.Schemas is { } schemas)
        {
            document.Components.Schemas = new SortedDictionary<string, IOpenApiSchema>(schemas, StringComparer.Ordinal);
        }

        document.Tags = new HashSet<OpenApiTag>(Tags.Select(t => new OpenApiTag { Name = t.Key, Description = t.Value }));
        return Task.CompletedTask;
    }

    private static void ApplySecurity(OpenApiDocument document, OpenApiOperation operation)
    {
        if (operation.Metadata?.TryGetValue(AccessTokenKey, out var value) != true || value is not bool required)
        {
            return;
        }

        operation.Security = [];
        if (!required)
        {
            // Requisito vacío: la operación también se puede llamar sin token.
            operation.Security.Add(new OpenApiSecurityRequirement());
        }

        operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] });
    }
}
