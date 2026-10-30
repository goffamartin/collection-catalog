using CollectionCatalog.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace CollectionCatalog.Web.Services.Users;

/// <summary>
/// CLI command <c>user-set-password</c> – creates the user (when missing) and sets their password:
/// <code>
/// dotnet CollectionCatalog.Web.dll user-set-password --username &lt;name&gt; [--name &lt;display name&gt;]
/// docker compose run --rm -it web user-set-password --username admin
/// </code>
/// The password is read from the console (hidden), so it does not stay in the command history. A forgotten password is reset the same way.
/// </summary>
public static class UserSetPasswordCommand
{
    public const string Name = "user-set-password";

    public static Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output) =>
        RunAsync(services, args, output, () => ReadConfirmedPassword(output));

    /// <param name="readPassword">Source of the password; <c>null</c> = the two entries did not match (tests supply the password directly).</param>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, Func<string?> readPassword)
    {
        string? userName = null;
        string? displayName = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--username" when i + 1 < args.Length:
                    userName = args[++i].Trim();
                    break;
                case "--name" when i + 1 < args.Length:
                    displayName = args[++i].Trim();
                    break;
                default:
                    return await UsageAsync(output, $"Unknown or incomplete argument: {args[i]}");
            }
        }

        if (string.IsNullOrEmpty(userName))
        {
            return await UsageAsync(output, "Missing --username.");
        }

        if (readPassword() is not { } password)
        {
            await output.WriteLineAsync("The passwords do not match.");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByNameAsync(userName);
        var created = user is null;
        if (user is null)
        {
            user = new AppUser { UserName = userName, DisplayName = displayName };
            if (!await CheckAsync(await userManager.CreateAsync(user, password), output))
            {
                return 1;
            }
        }
        else
        {
            if (displayName is not null)
            {
                user.DisplayName = displayName;
            }

            // Reset without knowing the old password; changes the security stamp, so other devices are signed out too.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            if (!await CheckAsync(await userManager.ResetPasswordAsync(user, token, password), output))
            {
                return 1;
            }
            await userManager.SetLockoutEndDateAsync(user, null);
        }

        await output.WriteLineAsync(created ? $"User {userName} created." : $"Password of user {userName} changed.");
        return 0;
    }

    /// <summary>Password from the console; interactively it is entered twice to catch typos.</summary>
    private static string? ReadConfirmedPassword(TextWriter output)
    {
        var password = ReadPassword(output, "Password: ");
        return Console.IsInputRedirected || ReadPassword(output, "Password again: ") == password ? password : null;
    }

    private static string ReadPassword(TextWriter output, string prompt)
    {
        // Non-interactive input (e.g. `echo password | …`) – just read a line.
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? "";
        }

        output.Write(prompt);
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                output.WriteLine();
                return password.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }

    private static async Task<bool> CheckAsync(IdentityResult result, TextWriter output)
    {
        foreach (var error in result.Errors)
        {
            await output.WriteLineAsync(error.Description);
        }
        return result.Succeeded;
    }

    private static async Task<int> UsageAsync(TextWriter output, string error)
    {
        await output.WriteLineAsync(error);
        await output.WriteLineAsync($"Usage: {Name} --username <name> [--name <display name>]");
        return 2;
    }
}
