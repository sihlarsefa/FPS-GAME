using FluentAssertions;
using Harekat.ClientSdk;

namespace Harekat.Tests.Api;

public class ClientSdkSmokeTests
{
    [Fact]
    public void Client_RequiresAuthBeforeMe()
    {
        using var client = new HarekatClient("http://localhost:9");
        var act = () => client.GetMeAsync();
        act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void DtoDefaults_AreSafe()
    {
        var p = new PlayerProfile();
        p.Username.Should().BeEmpty();
        var s = new SquadInfo();
        s.MemberIds.Should().BeEmpty();
    }
}
