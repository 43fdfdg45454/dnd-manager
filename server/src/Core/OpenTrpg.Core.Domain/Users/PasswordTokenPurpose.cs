namespace OpenTrpg.Core.Domain.Users;

public enum PasswordTokenPurpose
{
    /// <summary>Account setup after an admin creates the user.</summary>
    Setup = 0,

    /// <summary>"Forgot my password" flow.</summary>
    Reset = 1,
}
