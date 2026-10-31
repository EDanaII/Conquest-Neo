namespace Conquest.CLI;

/// <summary>Keyboard and screen boundary; redirected streams use plain screen snapshots.</summary>
public interface IClassicTerminal {
    int ReadKey();
    void Present(string[] screen);
    void MoveCursor(int column, int row) { }
    void Clear() { }
}

public sealed class ClassicConsoleTerminal : IClassicTerminal {
    private string[]? _previous;
    public void Clear() { _previous = null; }
    public void MoveCursor(int column, int row) {
        try {
            if (!Console.IsOutputRedirected && Console.WindowWidth >= 80 && Console.WindowHeight >= 25)
                Console.SetCursorPosition(column, row);
        }
        catch (IOException) { _previous = null; }
        catch (ArgumentOutOfRangeException) { _previous = null; }
    }
    public int ReadKey() {
        if (Console.IsInputRedirected) return Console.Read();
        while (true) {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) return '\n';
            if (key.Key == ConsoleKey.Backspace) return '\b';
            if (key.KeyChar != '\0') return key.KeyChar;
        }
    }

    public void Present(string[] screen) {
        try {
            if (!Console.IsOutputRedirected && Console.WindowWidth >= 80 && Console.WindowHeight >= 25) {
                if (_previous == null) Console.Clear();
                for (int y = 0; y < screen.Length; y++) {
                    if (_previous != null && _previous[y] == screen[y]) continue;
                    Console.SetCursorPosition(0, y);
                    Console.Write(screen[y]);
                }
                Console.SetCursorPosition(1, 18);
                _previous = screen;
                return;
            }
        }
        catch (IOException) { _previous = null; }
        catch (ArgumentOutOfRangeException) { _previous = null; }
        _previous = null;
        Console.WriteLine(string.Join(Environment.NewLine, screen));
    }
}
