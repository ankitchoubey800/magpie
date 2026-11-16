using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Magpie.Accounts.Tests.Integration;

[Collection(AccountsApiCollection.Name)]
public class PasswordResetTests(AccountsApiFactory factory)
{
    private const string OldPassword = "correct horse battery";
    private const string NewPassword = "brand new staple gun";

    private static string NewEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    private ApiSession NewSession() => new(factory.CreateBrowserLikeClient());

    private async Task<(string ReaderId, string Token)> RequestResetAsync(string email)
    {
        var response = await NewSession().PostAsync("/api/accounts/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return ApiSession.ReadResetLink(factory.Emails.SentTo(email)[^1].TextBody);
    }

    [Fact]
    public async Task A_reset_changes_the_password_and_ends_every_existing_session()
    {
        var email = NewEmail();
        var signedIn = NewSession();
        await signedIn.RegisterAndConfirmAsync(factory.Emails, email, OldPassword);
        await signedIn.PostAsync("/api/accounts/sign-in", new { email, password = OldPassword });
        Assert.Equal(HttpStatusCode.OK, (await signedIn.Client.GetAsync("/api/accounts/me")).StatusCode);

        var (readerId, token) = await RequestResetAsync(email);
        var reset = await NewSession().PostAsync("/api/accounts/reset-password", new { readerId, token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // The old password is dead, the new one works...
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await NewSession().PostAsync("/api/accounts/sign-in", new { email, password = OldPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await NewSession().PostAsync("/api/accounts/sign-in", new { email, password = NewPassword })).StatusCode);

        // ...and the browser that was signed in BEFORE the reset has been signed out.
        Assert.Equal(HttpStatusCode.Unauthorized, (await signedIn.Client.GetAsync("/api/accounts/me")).StatusCode);
    }

    [Fact]
    public async Task A_reset_link_works_only_once()
    {
        var email = NewEmail();
        await NewSession().RegisterAndConfirmAsync(factory.Emails, email, OldPassword);
        var (readerId, token) = await RequestResetAsync(email);

        var first = await NewSession().PostAsync("/api/accounts/reset-password", new { readerId, token, newPassword = NewPassword });
        var second = await NewSession().PostAsync("/api/accounts/reset-password", new { readerId, token, newPassword = "yet another password" });

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("This reset link does not work", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Asking_for_a_reset_for_an_unknown_email_answers_the_same_and_sends_nothing()
    {
        var email = NewEmail();

        var response = await NewSession().PostAsync("/api/accounts/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task A_weak_new_password_is_refused_and_the_link_stays_usable()
    {
        var email = NewEmail();
        await NewSession().RegisterAndConfirmAsync(factory.Emails, email, OldPassword);
        var (readerId, token) = await RequestResetAsync(email);

        var weak = await NewSession().PostAsync("/api/accounts/reset-password", new { readerId, token, newPassword = "short" });
        var good = await NewSession().PostAsync("/api/accounts/reset-password", new { readerId, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var problem = await weak.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").GetProperty("password").GetArrayLength() > 0);
        Assert.Equal(HttpStatusCode.NoContent, good.StatusCode);
    }

    [Fact]
    public async Task A_reset_lifts_a_lockout()
    {
        var email = NewEmail();
        var session = NewSession();
        await session.RegisterAndConfirmAsync(factory.Emails, email, OldPassword);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            await session.PostAsync("/api/accounts/sign-in", new { email, password = "wrong guess" });
        }

        // Locked: even the right password is refused.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.PostAsync("/api/accounts/sign-in", new { email, password = OldPassword })).StatusCode);

        var (readerId, token) = await RequestResetAsync(email);
        await NewSession().PostAsync("/api/accounts/reset-password", new { readerId, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent,
            (await NewSession().PostAsync("/api/accounts/sign-in", new { email, password = NewPassword })).StatusCode);
    }
}
