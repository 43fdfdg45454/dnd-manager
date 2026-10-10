using System.Collections.Concurrent;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace OpenTrpg.Core.Api.Filters;

/// <summary>
/// Validates every endpoint argument that has a registered FluentValidation validator and returns
/// <c>400</c> ProblemDetails with field errors (camelCase keys) when validation fails.
/// </summary>
public sealed class ValidationFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypes = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = ValidatorTypes.GetOrAdd(argument.GetType(), t => typeof(IValidator<>).MakeGenericType(t));
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return ValidationProblems.From(result);
            }
        }

        return await next(context);
    }
}

/// <summary>The <c>400</c> ProblemDetails for a failed FluentValidation result (camelCase field keys).</summary>
public static class ValidationProblems
{
    public static IResult From(ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return TypedResults.ValidationProblem(errors, title: "Uno o más campos no son válidos.");
    }
}
