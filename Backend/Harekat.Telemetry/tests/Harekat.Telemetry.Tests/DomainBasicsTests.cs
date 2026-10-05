using Harekat.Telemetry.Domain.Catalogs;
using Harekat.Telemetry.Domain.Constants;
using Harekat.Telemetry.Domain.ValueObjects;

namespace Harekat.Telemetry.Tests.DomainTests;

public class DomainBasicsTests
{
    [Fact]
    public void WeaponIds_TumKimliklerMevcut()
    {
        Assert.Equal(10, WeaponIds.All.Count);
        Assert.True(WeaponIds.IsKnown("pistol_sar9"));
        Assert.True(WeaponIds.IsKnown("ar_mpt76"));
        Assert.True(WeaponIds.IsKnown("sg_escort"));
        Assert.False(WeaponIds.IsKnown("ak47"));
        Assert.False(WeaponIds.IsKnown(null));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(-512, -512, true)]
    [InlineData(512, 512, true)]
    [InlineData(-513, 0, false)]
    [InlineData(0, 600, false)]
    public void MapBounds_Contains(float x, float z, bool expected) =>
        Assert.Equal(expected, MapBounds.Contains(x, z));

    [Fact]
    public void MapBounds_ToGrid_10m()
    {
        var (gx, gz) = MapBounds.ToGrid(-512, -512);
        Assert.Equal(0, gx);
        Assert.Equal(0, gz);

        var (gx2, gz2) = MapBounds.ToGrid(0, 0);
        Assert.Equal(51, gx2);
        Assert.Equal(51, gz2);

        Assert.Equal(102, MapBounds.GridDimension());
    }

    [Fact]
    public void WorldPosition_Mesafe()
    {
        var a = new WorldPosition(0, 0, 0);
        var b = new WorldPosition(3, 4, 0);
        Assert.Equal(5f, a.DistanceTo(b), 3);
        Assert.Equal(3f, a.HorizontalDistanceTo(b), 3);
    }
}
