// Conquest.Core
using Conquest.Core;

public sealed class CommandRouter {
    private readonly Dictionary<string, ICommandHandler> _h;
    public CommandRouter(IEnumerable<ICommandHandler> handlers)
        => _h = handlers.ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryRun(string name, string[] args, IGameIO io)
        => _h.TryGetValue(name, out var h) ? (h.Run(args, io), true).Item2 : false;
}
