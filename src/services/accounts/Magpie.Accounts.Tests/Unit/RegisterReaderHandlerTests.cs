using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.Registration;
using Magpie.Accounts.Tests.Fakes;

namespace Magpie.Accounts.Tests.Unit;

public class RegisterReaderHandlerTests
{
    private const string GoodPassword = "correct horse battery";

    private readonly FakeReaderAccounts _accounts = new();
    private readonly CapturingEmailSender _emails = new();
    private readonly RegisterReaderHandler _handler;

    public RegisterReaderHandlerTests()
    {
        _handler = new RegisterReaderHandler(_accounts, _emails, new AccountLinks(new Uri("https://magpie.test")));
    }

    [Fact]
    public async Task A_new_reader_gets_an_account_and_a_confirmation_link()
    {
        var outcome = await _handler.HandleAsync("asha@example.com", GoodPassword, CancellationToken.None);

        Assert.True(outcome.Accepted);
        Assert.True(_accounts.Exists("asha@example.com"));
        var email = Assert.Single(_emails.Sent);
        Assert.Equal("asha@example.com", email.To);
        Assert.Contains("https://magpie.test/confirm-email?readerId=", email.TextBody);
    }

    [Fact]
    public async Task Registering_an_existing_confirmed_email_looks_identical_to_the_caller()
    {
        await _handler.HandleAsync("asha@example.com", GoodPassword, CancellationToken.None);
        _accounts.MarkConfirmed("asha@example.com");

        var outcome = await _handler.HandleAsync("asha@example.com", "a different password", CancellationToken.None);

        // Same answer as for a brand-new address...
        Assert.Equal(RegisterOutcome.CheckYourEmail, outcome);
        // ...and the truth goes only to the inbox of the address's owner.
        Assert.Equal(2, _emails.Sent.Count);
        Assert.Contains("already", _emails.Sent[1].Subject);
        Assert.DoesNotContain("confirm-email", _emails.Sent[1].TextBody);
    }

    [Fact]
    public async Task Registering_again_before_confirming_sends_the_link_again()
    {
        await _handler.HandleAsync("asha@example.com", GoodPassword, CancellationToken.None);

        var outcome = await _handler.HandleAsync("asha@example.com", GoodPassword, CancellationToken.None);

        Assert.True(outcome.Accepted);
        Assert.Equal(2, _emails.Sent.Count);
        Assert.All(_emails.Sent, email => Assert.Contains("confirm-email", email.TextBody));
    }

    [Fact]
    public async Task A_rejected_password_is_reported_and_no_email_is_sent()
    {
        var outcome = await _handler.HandleAsync("asha@example.com", "short", CancellationToken.None);

        Assert.False(outcome.Accepted);
        Assert.NotEmpty(outcome.Errors);
        Assert.False(_accounts.Exists("asha@example.com"));
        Assert.Empty(_emails.Sent);
    }

    [Theory]
    [InlineData("not-an-email", GoodPassword)]
    [InlineData("asha@example.com", "")]
    [InlineData(null, null)]
    public async Task Missing_or_malformed_input_is_rejected_before_anything_is_stored(string? email, string? password)
    {
        var outcome = await _handler.HandleAsync(email, password, CancellationToken.None);

        Assert.False(outcome.Accepted);
        Assert.Empty(_emails.Sent);
    }
}
