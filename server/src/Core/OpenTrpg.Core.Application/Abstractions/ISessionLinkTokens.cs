namespace OpenTrpg.Core.Application.Abstractions;

/// <summary>
/// Signed tokens carried by the links of session emails, so a member can see a session and answer
/// attendance without signing in. A token only proves "this link was issued for this user and this
/// session"; it grants nothing else.
/// </summary>
public interface ISessionLinkTokens
{
    /// <summary>Token for the given session and user (the user id plus an HMAC-SHA256 signature, URL-safe).</summary>
    string Create(Guid sessionId, Guid userId);

    /// <summary>Validates the token for the session and returns the user it was issued to.</summary>
    bool TryValidate(Guid sessionId, string? token, out Guid userId);
}
