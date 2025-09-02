// Conquest.Core
using System;
using System.Collections.Generic;
using System.Linq;

namespace Conquest.Core;

public sealed class CommandRouter {
    private readonly Dictionary<string, ICommandHandler> _h;

    public CommandRouter(IEnumerable<ICommandHandler> handlers)
        => _h = handlers.ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryRun(string name, string[] args, IGameIO io) {
        if (_h.TryGetValue(name, out var h)) {
            h.Run(args, io);   // Run returns void — just call it, then return true
            return true;
        }
        return false;
    }
}
