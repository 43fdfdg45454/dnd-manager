using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Auth;
using OpenTrpg.Core.Application.Users;
using OpenTrpg.Core.Infrastructure.Email.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace OpenTrpg.Core.Api.Tests;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens_and_user()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email.ToUpperInvariant(), password = user.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.InRange(auth.AccessTokenExpiresAt, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        Assert.Equal(user.User.Id, auth.User.Id);
        Assert.Equal("User", auth.User.Role);
        Assert.NotNull(auth.User.LastLoginAt);

        var jwt = new JsonWebToken(auth.AccessToken);
        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal(user.User.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal(user.Email, jwt.GetClaim("email").Value);
        Assert.Equal("Test User", jwt.GetClaim("name").Value);
        Assert.Equal("User", jwt.GetClaim("role").Value);
    }

    [Fact]
    public async Task Login_with_wrong_password_or_unknown_email_returns_401_problem()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "not-the-password" });
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.com", password = "whatever-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(401, (await wrongPassword.ReadProblemAsync()).GetProperty("status").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_400_with_field_errors()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("email"));
        Assert.True(problem.HasFieldError("password"));
    }

    [Fact]
    public async Task Login_before_setting_a_password_returns_401()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"pending-{Guid.NewGuid():N}@example.com";
        (await admin.PostAsJsonAsync("/api/v1/admin/users", new { email, displayName = "Pending", role = "User" })).EnsureSuccessStatusCode();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "anything-1234" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_previous_one_stops_working()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        var first = await client.LoginAsync(user.Email, user.Password);

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.False(string.IsNullOrEmpty(second.AccessToken));

        var reused = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // Reusing a revoked token revokes the whole family, including the one just issued.
        var afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_unknown_or_expired_token_returns_401()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        var auth = await client.LoginAsync(user.Email, user.Password);

        var unknown = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE RefreshTokens SET ExpiresAt = '2000-01-01 00:00:00+00:00' WHERE UserId = {0}",
            user.User.Id.ToString().ToUpperInvariant()));
        var expired = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        var auth = await client.LoginAsync(user.Email, user.Password);

        var anonymous = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Me_requires_a_jwt_and_returns_the_current_user()
    {
        var user = await factory.CreateUserAsync();

        var anonymous = await factory.CreateClient().GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var client = await factory.CreateClientForAsync(user);
        var me = await client.GetFromJsonAsync<UserDto>("/api/v1/auth/me");
        Assert.NotNull(me);
        Assert.Equal(user.User.Id, me.Id);
        Assert.Equal(user.Email, me.Email);
        Assert.True(me.HasPassword);
        Assert.True(me.IsActive);
    }

    [Fact]
    public async Task Me_rejects_a_token_signed_with_another_key()
    {
        var client = factory.CreateClient();
        var forged = new JsonWebTokenHandler().CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "dnd-companion",
            Audience = "dnd-companion-app",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "Admin" },
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey("another-secret-of-at-least-32-characters!!"u8.ToArray()),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_returns_202_for_an_unknown_email_without_sending_anything()
    {
        var sentBefore = factory.Emails.Messages.Count;

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/password/forgot", new { email = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(sentBefore, factory.Emails.Messages.Count);
    }

    [Fact]
    public async Task Forgot_password_sends_a_reset_link_that_sets_a_new_password_and_closes_sessions()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        var session = await client.LoginAsync(user.Email, user.Password);

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email = user.Email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        var email = factory.Emails.LastSentTo(user.Email);
        Assert.Equal(AccountEmailTemplates.ResetSubject, email.Subject);

        const string newPassword = "brand-new-password-42";
        var set = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token = FakeEmailSender.ExtractToken(email), password = newPassword });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await client.LoginAsync(user.Email, newPassword);

        var oldRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
    }

    [Fact]
    public async Task Set_password_rejects_invalid_used_or_expired_tokens()
    {
        var client = factory.CreateClient();

        var invalid = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token = "invalid-token", password = "long-enough-password" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await invalid.ReadProblemAsync()).HasFieldError("token"));

        // A token that was already used (by CreateUserAsync) cannot be reused.
        var user = await factory.CreateUserAsync();
        var reused = await client.PostAsJsonAsync(
            "/api/v1/auth/password/set",
            new { token = factory.Emails.LastTokenSentTo(user.Email), password = "another-password-123" });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);

        // An expired token is rejected.
        await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email = user.Email });
        var resetToken = factory.Emails.LastTokenSentTo(user.Email);
        await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE PasswordTokens SET ExpiresAt = '2000-01-01 00:00:00+00:00' WHERE UserId = {0}",
            user.User.Id.ToString().ToUpperInvariant()));
        var expired = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token = resetToken, password = "another-password-123" });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
    }

    [Fact]
    public async Task Set_password_rejects_a_weak_password_without_consuming_the_token()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"weak-{Guid.NewGuid():N}@example.com";
        (await admin.PostAsJsonAsync("/api/v1/admin/users", new { email, displayName = "Weak", role = "User" })).EnsureSuccessStatusCode();
        var token = factory.Emails.LastTokenSentTo(email);
        var client = factory.CreateClient();

        var weak = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token, password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await weak.ReadProblemAsync()).HasFieldError("password"));

        var ok = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token, password = "0123456789" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    }
}
