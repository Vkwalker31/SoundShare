using FluentValidation;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Queue;

namespace SoundShare.Application.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 32).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => new RegisterRequest(x.Username, x.Email, x.Password)).SetValidator(new RegisterRequestValidator());
        RuleFor(x => x.Role).NotEmpty().Must(r => r is "Admin" or "User").WithMessage("Role must be Admin or User.");
    }
}

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role).NotEmpty().Must(r => r is "Admin" or "User").WithMessage("Role must be Admin or User.");
    }
}

public sealed class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(100);
    }
}

public sealed class JoinByCodeRequestValidator : AbstractValidator<JoinByCodeRequest>
{
    public JoinByCodeRequestValidator()
    {
        RuleFor(x => x.InviteCode).NotEmpty().MaximumLength(16);
    }
}

public sealed class BanRequestValidator : AbstractValidator<BanRequest>
{
    public BanRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(1, 525_600).When(x => x.DurationMinutes is not null);
    }
}

public sealed class VoteRequestValidator : AbstractValidator<VoteRequest>
{
    public VoteRequestValidator()
    {
        RuleFor(x => x.Value).Must(QueueRules.IsValidVote).WithMessage("Vote must be -1, 0 or 1.");
    }
}

public sealed class CreatePlaylistRequestValidator : AbstractValidator<CreatePlaylistRequest>
{
    public CreatePlaylistRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(100);
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
