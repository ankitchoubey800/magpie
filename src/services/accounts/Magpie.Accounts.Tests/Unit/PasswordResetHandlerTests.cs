using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.PasswordReset;
using Magpie.Accounts.Application.Registration;
using Magpie.Accounts.Tests.Fakes;

namespace Magpie.Accounts.Tests.Unit;

public class PasswordResetHandlerTests
{
    private const string Password = "correct horse battery";

    private readonly FakeReaderAccounts _accounts = new();
    private readonly CapturingEmailSender _emails = new();
    private readonly AccountLinks _links = new(new Uri("https://magpie.test"));

    private async Task RegisterAsync(string email, bool confirm)
    {
        await new RegisterReaderHandler(_accounts, new CapturingEmailSender(), _links)
            .HandleAsync(email, Password, CancellationToken.None);
        if (confirm)
        {
            _accounts.MarkConfirmed(email);
        }
    }

    [Fact]
    public async Task A_confirmed_reader_gets_a_reset_link()
    {
        await RegisterAsync("asha@example.com", confirm: true);

        await new RequestPasswordResetHandler(_accounts, _emails, _links).HandleAsync("asha@example.com", CancellationToken.None);

        var email = Assert.Single(_emails.Sent);
        Assert.Contains("https://magpie.test/reset-password?readerId=", email.TextBody);
    }

    [Theory]
    [InlineData("nobody@example.com")]
    [InlineData("not-an-email")]
    [InlineData(null)]
    public async Task An_unknown_or_malformed_email_sends_nothing(string? email)
    {
        await new RequestPasswordResetHandler(_accounts, _emails, _links).HandleAsync(email, CancellationToken.None);

        Assert.Empty(_emails.Sent);
    }

    [Fact]
    public async Task An_unconfirmed_account_cannot_be_reset()
    {
        await RegisterAsync("asha@example.com", confirm: false);

        await new RequestPasswordResetHandler(_accounts, _emails, _links).HandleAsync("asha@example.com", CancellationToken.None);

        Assert.Empty(_emails.Sent);
    }

    [Theory]
    [InlineData(null, "token")]
    [InlineData("reader", null)]
    [InlineData(" ", " ")]
    public async Task A_link_with_missing_parts_is_invalid(string? readerId, string? token)
    {
        var result = await new ResetPasswordHandler(_accounts).HandleAsync(readerId, token, Password, CancellationToken.None);

        Assert.Equal(ResetPasswordStatus.InvalidLink, result.Status);
    }

    [Fact]
    public async Task An_empty_new_password_is_rejected_before_the_token_is_spent()
    {
        var result = await new ResetPasswordHandler(_accounts).HandleAsync("reader", "token", "", CancellationToken.None);

        Assert.Equal(ResetPasswordStatus.RejectedPassword, result.Status);
    }
}
