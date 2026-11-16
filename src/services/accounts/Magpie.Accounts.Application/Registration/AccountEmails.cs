using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Registration;

/// <summary>The exact words of the emails this service sends. One place to edit them.</summary>
public static class AccountEmails
{
    public static EmailMessage ConfirmYourEmail(EmailAddress to, Uri confirmLink) => new(
        to.Value,
        "Confirm your email for Magpie",
        $"""
         Welcome to Magpie.

         Open this link to confirm your email address:
         {confirmLink}

         The link works for 24 hours. If you did not create a Magpie account, ignore this email and nothing will happen.
         """);

    public static EmailMessage AlreadyRegistered(EmailAddress to, Uri signInLink) => new(
        to.Value,
        "You already have a Magpie account",
        $"""
         Someone (probably you) tried to create a Magpie account with this email address, but one already exists.

         You can sign in here:
         {signInLink}

         If this was not you, there is nothing you need to do. Your account has not been changed.
         """);

    public static EmailMessage ResetYourPassword(EmailAddress to, Uri resetLink) => new(
        to.Value,
        "Reset your Magpie password",
        $"""
         Someone (probably you) asked to reset the password for this Magpie account.

         Open this link to choose a new password:
         {resetLink}

         The link works for 1 hour and only once. If you did not ask for this, ignore this email. Your password has not been changed.
         """);
}
