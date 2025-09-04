namespace Conquest.Core;

public interface IGameIO {
    string ReadLine( );
    void Write(string text);
    void WriteLine(string text = "");
}
