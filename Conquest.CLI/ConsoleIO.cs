using Conquest.Core;
namespace Conquest.CLI;

class ConsoleIO : IGameIO {
    public string ReadLine( ) => Console.ReadLine( ) ?? "";
    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text = "") => Console.WriteLine(text);
}