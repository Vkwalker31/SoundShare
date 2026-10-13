namespace SoundShare.Application.Common;

public sealed class AccountBlockedException : Exception
{
    public AccountBlockedException()
    {
    }

    public AccountBlockedException(string message)
        : base(message)
    {
    }

    public AccountBlockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
