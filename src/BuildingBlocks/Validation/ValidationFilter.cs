using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Nexgate.BuildingBlocks.Validation;

public static class ValidationExtensions
{
    /// <summary>Valida con DataAnnotations el argumento de tipo T y responde 400 con el detalle de cada campo inválido.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var argument = context.Arguments.OfType<T>().FirstOrDefault();
            if (argument is null)
            {
                return await next(context);
            }

            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true))
            {
                return await next(context);
            }

            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [string.Empty])
                    .Select(member => (Member: member, Message: r.ErrorMessage ?? "Valor inválido")))
                .GroupBy(x => x.Member)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

            var http = context.HttpContext;
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = new HttpValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Error de validación"
                }
            });
            return Results.Empty;
        });
}
