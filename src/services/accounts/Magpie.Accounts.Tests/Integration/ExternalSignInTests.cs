using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Magpie.Accounts.Tests.Integration;

// "Continue with Google", with a stand-in for Google itself (FakeGoogleHandler).
// The client follows redirects like a browser, so each test looks at where the
// journey ENDED (the last address) and at who the server now thinks we are.
[Collection(AccountsApiCollection.Name)]
public class ExternalSignInTests(AccountsApiFactory factory)
{
    private const string Password = "correct horse battery";

    private static string NewEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    private static string NewGoogleId() => $"google-{Guid.NewGuid():N}";

    private static string LandedOn(HttpResponseMessage response) => response.RequestMessage!.RequestUri!.PathAndQuery;

    private static async Task<JsonElement?> WhoAmIAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/accounts/me");
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<JsonElement>()
            : null;
    }

    [Fact]
    public async Task The_sign_in_page_is_told_that_google_is_available()
    {
        using var client = factory.CreateBrowserLikeClient();

        var providers = await client.GetFromJsonAsync<string[]>("/api/accounts/external/providers");

        Assert.Equal(["Google"], providers);
    }

    [Fact]
    public async Task A_new_visitor_gets_an_account_and_is_signed_in()
    {
        var email = NewEmail();
        using var client = factory.CreateBrowserLikeClient();

        var response = await client.GetAsync(FakeGoogleHandler.StartUrl(NewGoogleId(), email));

        Assert.Equal("/welcome", LandedOn(response));
        var me = await WhoAmIAsync(client);
        Assert.Equal(email, me?.GetProperty("email").GetString());
    }

    [Fact]
    public async Task A_returning_visitor_lands_in_the_same_account()
    {
        var email = NewEmail();
        var googleId = NewGoogleId();
        using var firstVisit = factory.CreateBrowserLikeClient();
        await firstVisit.GetAsync(FakeGoogleHandler.StartUrl(googleId, email));
        var firstId = (await WhoAmIAsync(firstVisit))?.GetProperty("id").GetString();

        using var secondVisit = factory.CreateBrowserLikeClient();
        await secondVisit.GetAsync(FakeGoogleHandler.StartUrl(googleId, email));
        var secondId = (await WhoAmIAsync(secondVisit))?.GetProperty("id").GetString();

        Assert.NotNull(firstId);
        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task An_existing_password_account_is_never_linked_just_because_the_email_matches()
    {
        var email = NewEmail();
        await new ApiSession(factory.CreateBrowserLikeClient()).RegisterAndConfirmAsync(factory.Emails, email, Password);

        // Someone arrives from "Google" claiming that same email address.
        using var client = factory.CreateBrowserLikeClient();
        var response = await client.GetAsync(FakeGoogleHandler.StartUrl(NewGoogleId(), email));

        Assert.Equal("/sign-in?link=pending", LandedOn(response));
        Assert.Null(await WhoAmIAsync(client));
    }

    [Fact]
    public async Task After_proving_the_password_the_account_is_linked_and_google_then_signs_in_directly()
    {
        var email = NewEmail();
        var googleId = NewGoogleId();
        await new ApiSession(factory.CreateBrowserLikeClient()).RegisterAndConfirmAsync(factory.Emails, email, Password);

        // Arrive from Google (not linked yet), then prove ownership with the password.
        var session = new ApiSession(factory.CreateBrowserLikeClient());
        await session.Client.GetAsync(FakeGoogleHandler.StartUrl(googleId, email));
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostAsync("/api/accounts/sign-in", new { email, password = Password })).StatusCode);
        session.ForgetAntiforgeryToken();
        var passwordAccountId = (await WhoAmIAsync(session.Client))?.GetProperty("id").GetString();

        var link = await session.PostAsync("/api/accounts/external/link");
        Assert.Equal(HttpStatusCode.NoContent, link.StatusCode);

        // A fresh browser: Google alone is now enough, and it is the SAME account.
        using var later = factory.CreateBrowserLikeClient();
        var response = await later.GetAsync(FakeGoogleHandler.StartUrl(googleId, email));
        Assert.Equal("/welcome", LandedOn(response));
        Assert.Equal(passwordAccountId, (await WhoAmIAsync(later))?.GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_pending_google_identity_cannot_be_linked_to_somebody_elses_account()
    {
        var victimEmail = NewEmail();
        var attackerEmail = NewEmail();
        await new ApiSession(factory.CreateBrowserLikeClient()).RegisterAndConfirmAsync(factory.Emails, victimEmail, Password);
        var attacker = new ApiSession(factory.CreateBrowserLikeClient());
        await attacker.RegisterAndConfirmAsync(factory.Emails, attackerEmail, Password);

        // The attacker has a pending Google identity for the VICTIM's email,
        // then signs in to their OWN account and tries to link.
        await attacker.Client.GetAsync(FakeGoogleHandler.StartUrl(NewGoogleId(), victimEmail));
        await attacker.PostAsync("/api/accounts/sign-in", new { email = attackerEmail, password = Password });
        attacker.ForgetAntiforgeryToken();

        var link = await attacker.PostAsync("/api/accounts/external/link");

        Assert.Equal(HttpStatusCode.BadRequest, link.StatusCode);
    }

    [Fact]
    public async Task An_unverified_email_from_the_provider_is_refused()
    {
        using var client = factory.CreateBrowserLikeClient();

        var response = await client.GetAsync(FakeGoogleHandler.StartUrl(NewGoogleId(), NewEmail(), emailVerified: false));

        Assert.Equal("/sign-in?error=external", LandedOn(response));
        Assert.Null(await WhoAmIAsync(client));
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("//evil.example/steal")]
    public async Task The_return_address_can_only_be_a_page_on_our_own_site(string returnUrl)
    {
        using var client = factory.CreateBrowserLikeClient();

        var response = await client.GetAsync(FakeGoogleHandler.StartUrl(NewGoogleId(), NewEmail(), returnUrl: returnUrl));

        Assert.Equal("localhost", response.RequestMessage!.RequestUri!.Host);
        Assert.Equal("/", LandedOn(response));
    }
}
