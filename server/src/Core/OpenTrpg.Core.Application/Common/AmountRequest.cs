using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;
using OpenTrpg.Core.Domain.Rules;

namespace OpenTrpg.Core.Application.Common;

/// <summary>Uses to spend or restore (default 1). The body is optional.</summary>
public sealed record AmountRequest(int Amount = 1);

public sealed class AmountRequestValidator : AbstractValidator<AmountRequest>
{
    public const int MaxAmount = 999;

    public AmountRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(1, MaxAmount).WithMessage($"La cantidad debe estar entre 1 y {MaxAmount}.");
    }
}
