using Conquest.Core;
using Conquest.Tests.TestHelpers;
using Xunit;

namespace Conquest.Tests;

public sealed class RouterTests {
    private sealed class FakeHandler : ICommandHandler {
        public string Name { get; }
        public int Calls { get; private set; }
        public List<string[]> SeenArgs { get; } = new( );

        public FakeHandler(string name) => Name = name;
        public void Run(string[] args, IGameIO io) {
            Calls++;
            SeenArgs.Add(args);
        }
    }

    [Fact]
    public void TryRun_Invokes_Handler_CaseInsensitive( ) {
        var h = new FakeHandler("genstars");
        var r = new CommandRouter(new[] { h });

        var ok = r.TryRun("GenStars", new[] { "--count", "5" }, new DummyIO( ));

        Assert.True(ok);
        Assert.Equal(1, h.Calls);
        Assert.Single(h.SeenArgs);
        Assert.Equal("--count", h.SeenArgs[0][0]);
        Assert.Equal("5", h.SeenArgs[0][1]);
    }

    [Fact]
    public void TryRun_Unknown_ReturnsFalse( ) {
        var r = new CommandRouter(Array.Empty<ICommandHandler>( ));
        Assert.False(r.TryRun("nope", Array.Empty<string>( ), new DummyIO( )));
    }
}
