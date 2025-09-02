using Conquest.Core;

namespace Conquest.Tests.TestHelpers;

/// <summary>
/// Minimal command handler stub for router tests.
/// </summary>
public sealed class FakeHandler : ICommandHandler {
    private readonly Action<string[], IGameIO> _onRun;

    public string Name { get; }
    public int CallCount { get; private set; }

    public FakeHandler(string name, Action<string[], IGameIO> onRun) {
        Name = name;
        _onRun = onRun;
    }

    public void Run(string[] args, IGameIO io) {
        CallCount++;
        _onRun?.Invoke(args, io);
    }
}
