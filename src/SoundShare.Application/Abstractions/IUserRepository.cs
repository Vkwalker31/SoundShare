using SoundShare.Application.Common;
using SoundShare.Domain.Users;

namespace SoundShare.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken);

    Task<PagedResult<User>> ListAsync(SystemRole? role, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);

    void Add(User user);

    Task<RefreshToken?> GetRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(long userId, DateTime now, CancellationToken cancellationToken);

    void AddRefreshToken(RefreshToken token);
}
