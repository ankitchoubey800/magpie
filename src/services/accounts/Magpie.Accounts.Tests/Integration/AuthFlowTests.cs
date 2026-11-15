using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Magpie.Accounts.Tests.Integration;

// Real HTTP requests, real Identity, real PostgreSQL. Each test uses its own
// random email address, so tests can share one database without colliding.
[Collection(AccountsApiCollection.Name)]
public class AuthFlowTests(AccountsApiFactory factory)
{
    private const string Password = "correct horse battery";

    private static string NewEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    private ApiSession NewSession() => new(factory.CreateBrowserLikeClient());

    [Fact]
    public async Task Happy_path_register_confirm_sign_in_who_am_i_sign_out()
    {
        var email = NewEmail();
        var session = NewSession();

        // Register: accepted, and exactly one email arrives.
        var register = await session.PostAsync("/api/accounts/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        var message = Assert.Single(factory.Emails.SentTo(email));

        // Confirm with the link from the email.
        var (readerId, token) = ApiSession.ReadConfirmationLink(message.TextBody);
        var confirm = await session.PostAsync("/api/accounts/confirm-email", new { readerId, token });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        // Sign in: a session cookie with all three safety flags.
        var signIn = await session.PostAsync("/api/accounts/sign-in", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        var cookie = Assert.Single(signIn.Headers.GetValues("Set-Cookie"), value => value.StartsWith("magpie.session="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        session.ForgetAntiforgeryToken();

        // Who am I?
        var me = await session.Client.GetFromJsonAsync<JsonElement>("/api/accounts/me");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal(readerId, me.GetProperty("id").GetString());

        // Sign out, and the same browser is anonymous again.
        var signOut = await session.PostAsync("/api/accounts/sign-out");
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        var meAfter = await session.Client.GetAsync("/api/accounts/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [Fact]
    public async Task Failure_wrong_password_is_rejected_with_the_generic_message()
    {
        var email = NewEmail();
        var session = NewSession();
        await session.RegisterAndConfirmAsync(factory.Emails, email, Password);

        var response = await session.PostAsync("/api/accounts/sign-in", new { email, password = "not the password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Sign-in failed", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Failure_unknown_email_gets_exactly_the_same_answer_as_a_wrong_password()
    {
        var email = NewEmail();
        var session = NewSession();
        await session.RegisterAndConfirmAsync(factory.Emails, email, Password);

        var wrongPassword = await session.PostAsync("/api/accounts/sign-in", new { email, password = "not the password" });
        var unknownEmail = await session.PostAsync("/api/accounts/sign-in", new { email = NewEmail(), password = Password });

        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        var first = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var second = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("title").GetString(), second.GetProperty("title").GetString());
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Failure_cannot_sign_in_before_confirming_the_email()
    {
        var email = NewEmail();
        var session = NewSession();
        await session.PostAsync("/api/accounts/register", new { email, password = Password });

        var response = await session.PostAsync("/api/accounts/sign-in", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Failure_a_post_without_the_antiforgery_token_is_refused()
    {
        using var client = factory.CreateBrowserLikeClient();

        var response = await client.PostAsJsonAsync("/api/accounts/sign-in", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("antiforgery", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Registering_a_taken_email_answers_the_same_and_does_not_change_the_account()
    {
        var email = NewEmail();
        var session = NewSession();
        await session.RegisterAndConfirmAsync(factory.Emails, email, Password);

        var second = await session.PostAsync("/api/accounts/register", new { email, password = "attacker chosen password" });

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Contains("already", factory.Emails.SentTo(email)[^1].Subject);
        // The original password still works; the attacker's does not.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.PostAsync("/api/accounts/sign-in", new { email, password = "attacker chosen password" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostAsync("/api/accounts/sign-in", new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task A_weak_password_is_refused_with_a_validation_message()
    {
        var response = await NewSession().PostAsync("/api/accounts/register", new { email = NewEmail(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").GetProperty("registration").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Who_am_i_without_a_cookie_is_401()
    {
        using var client = factory.CreateBrowserLikeClient();

        var response = await client.GetAsync("/api/accounts/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
