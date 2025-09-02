// Conquest.Tests/ConfigDefaultsTests.cs
using Conquest.Core;
using Xunit;

namespace Conquest.Tests;

public sealed class ConfigDefaultsTests {
    [Fact]
    public void Default_Is_Valid_And_WithinBounds( ) {
        var c = GameConfig.Default;
        Assert.True(c.Width > 0 && c.Width <= Constants.Max.BoardSize);
        Assert.True(c.Height > 0 && c.Height <= Constants.Max.BoardSize);
        Assert.True(c.MaxTurns > 0 && c.MaxTurns <= Constants.Max.Turns);
        Assert.True(c.NumStars >= 0 && c.NumStars <= (long)c.Width * c.Height);
    }
}
