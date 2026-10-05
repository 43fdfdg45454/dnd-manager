using Dnd.Application.Users;

namespace Dnd.Application.Auth;

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserDto User);
