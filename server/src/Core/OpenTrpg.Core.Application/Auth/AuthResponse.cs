using OpenTrpg.Core.Application.Users;

namespace OpenTrpg.Core.Application.Auth;

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserDto User);
