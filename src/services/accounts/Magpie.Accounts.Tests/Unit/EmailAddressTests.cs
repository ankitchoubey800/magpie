using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests.Unit;

public class EmailAddressTests
{
    // [Theory] runs the same test once per [InlineData] line.
    [Theory]
    [InlineData("asha@example.com")]
    [InlineData("first.last+tag@mail.example.co.in")]
    [InlineData("  padded@example.com  ")]
    public void Accepts_reasonable_addresses(string input)
    {
        var accepted = EmailAddress.TryCreate(input, out var email);

        Assert.True(accepted);
        Assert.Equal(input.Trim(), email!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign.example.com")]
    [InlineData("two@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("@example.com")]
    [InlineData("asha@")]
    [InlineData("asha@localhost")]
    [InlineData("asha@.com")]
    [InlineData("asha@example.")]
    [InlineData("asha smith@example.com")]
    public void Rejects_addresses_that_cannot_work(string? input)
    {
        var accepted = EmailAddress.TryCreate(input, out var email);

        Assert.False(accepted);
        Assert.Null(email);
    }

    [Fact]
    public void Rejects_an_address_longer_than_the_standard_allows()
    {
        var tooLong = new string('a', EmailAddress.MaxLength) + "@example.com";

        Assert.False(EmailAddress.TryCreate(tooLong, out _));
    }
}
