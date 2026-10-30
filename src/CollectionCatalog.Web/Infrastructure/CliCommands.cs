using CollectionCatalog.Web.Services.Demo;
using CollectionCatalog.Web.Services.Users;

namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Administration commands run instead of the web server, e.g. <c>dotnet CollectionCatalog.Web.dll user-set-password …</c>.
/// They use the same configuration and database as the website (migrations are applied first).
/// </summary>
public static class CliCommands
{
    private static readonly Dictionary<string, Func<IServiceProvider, string[], TextWriter, Task<int>>> _commands = new()
    {
        [UserSetPasswordCommand.Name] = UserSetPasswordCommand.RunAsync,
        [DemoSeedCommand.Name] = DemoSeedCommand.RunAsync,
    };

    public static bool IsCommand(string[] args) => args.Length > 0 && _commands.ContainsKey(args[0]);

    public static Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output) =>
        _commands[args[0]](services, args, output);
}
