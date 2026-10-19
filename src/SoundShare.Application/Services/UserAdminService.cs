using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Users;

namespace SoundShare.Application.Services;

public sealed class UserAdminService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    TimeProvider time,
    RoomService rooms,
    IValidator<CreateUserRequest> createValidator,
    IValidator<ChangeRoleRequest> roleValidator,
    IValidator<DeleteAccountRequest> deleteValidator)
{
    public async Task<UserDto> GetAsync(long userId, CancellationToken cancellationToken) =>
        (await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.")).ToDto();

    public async Task<PagedResult<UserDto>> ListAsync(string? role, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var parsedRole = string.IsNullOrWhiteSpace(role) ? (SystemRole?)null : Roles.ParseSystemRole(role);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await users.ListAsync(parsedRole, isActive, page, pageSize, cancellationToken);
        return new PagedResult<UserDto>(result.Items.Select(u => u.ToDto()).ToList(), result.Total, page, pageSize);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await AuthService.EnsureUniqueAsync(users, request.Username, request.Email, cancellationToken);

        var user = new User(request.Username, request.Email, passwordHasher.Hash(request.Password), Roles.ParseSystemRole(request.Role), time.UtcNow());
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToDto();
    }

    public async Task ChangeRoleAsync(long userId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await roleValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        var role = Roles.ParseSystemRole(request.Role);

        if (user.Role == SystemRole.Admin && role != SystemRole.Admin && user.IsActive
            && await users.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            throw new ConflictException("Cannot remove the Admin role from the last administrator.");
        }

        user.ChangeRole(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task BlockAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        if (user.Role == SystemRole.Admin && user.IsActive && await users.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            throw new ConflictException("Cannot block the last administrator.");
        }

        var now = time.UtcNow();
        user.Block();
        foreach (var token in await users.GetActiveRefreshTokensAsync(userId, now, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        user.Unblock();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOwnAccountAsync(long userId, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await deleteValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new ForbiddenException("Password is incorrect.");
        }

        await unitOfWork.InTransactionAsync(
            async ct =>
            {
                await rooms.LeaveCurrentRoomAsync(userId, ct);
                var now = time.UtcNow();
                foreach (var token in await users.GetActiveRefreshTokensAsync(userId, now, ct))
                {
                    token.Revoke(now);
                }

                user.Anonymize("!deleted-account");
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
    }
}
