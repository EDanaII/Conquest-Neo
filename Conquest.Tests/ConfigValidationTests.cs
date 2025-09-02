using System;
using Conquest.Core;
using Xunit;

namespace Conquest.Tests;

public sealed class ConfigValidationTests {
    [Fact]
    public void Config_InvalidDimensions_Throws( ) {
        Assert.Throws<ArgumentOutOfRangeException>(( ) =>
            new GameConfig {
                Width = 0,                // invalid
                Height = 64,
                NumStars = 10,
                MaxTurns = 100
            }.Validate( )
        );
    }

    [Fact]
    public void Config_TooManyStars_ClampedToCells( ) {
        var cfg = new GameConfig {
            Width = 4,
            Height = 4,
            NumStars = 999,
            MaxTurns = 10
        }.Validate( );

        Assert.Equal(16, cfg.NumStars); // 4 * 4
    }

    [Fact]
    public void Config_NegativeStars_Throws( ) {
        Assert.Throws<ArgumentOutOfRangeException>(( ) =>
            new GameConfig {
                Width = 64,
                Height = 64,
                NumStars = -1,
                MaxTurns = 10
            }.Validate( )
        );
    }

    [Fact]
    public void Config_TurnsMustBePositive_Throws( ) {
        Assert.Throws<ArgumentOutOfRangeException>(( ) =>
            new GameConfig {
                Width = 64,
                Height = 64,
                NumStars = 10,
                MaxTurns = 0
            }.Validate( )
        );
    }
}
