using System.Collections.Concurrent;
using Conquest.Core;

namespace Conquest.Tests.TestHelpers;

/// <summary>
/// Scripted I/O for tests.
/// - Feed input lines in ctor
/// - Captures all writes in an in-memory buffer
/// - You can inspect OutputLines if you really need to,
///   but prefer asserting on state where possible.
/// </summary>
public sealed class DummyIO : IGameIO {
    private readonly ConcurrentQueue<string> _inputs = new( );
    private readonly List<string> _output = new( );

    public DummyIO(IEnumerable<string>? inputs = null) {
        if (inputs != null)
            foreach (var s in inputs) _inputs.Enqueue(s);
    }

    public string ReadLine( ) {
        if (_inputs.TryDequeue(out var s))
            return s;
        // If tests forgot to enqueue "quit", don't hang forever:
        return "quit";
    }

    public void Write(string text) {
        lock (_output) _output.Add(text);
    }

    public void WriteLine(string text = "") {
        lock (_output) _output.Add(text + "\n");
    }

    /// <summary>All captured output lines. Use sparingly; favor state assertions.</summary>
    public IReadOnlyList<string> OutputLines {
        get { lock (_output) return _output.ToArray( ); }
    }

    /// <summary>Add input lines after construction (optional).</summary>
    public void Enqueue(params string[] lines) {
        foreach (var s in lines) _inputs.Enqueue(s);
    }
}
