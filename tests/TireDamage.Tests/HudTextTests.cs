namespace TireDamage.Tests
{
    public class HudTextTests
    {
        private static string Surface(params int[] labels) => HudText.MainSurface(labels);

        [Theory]
        [InlineData(0x2, "Grass")]
        [InlineData(0x4, "Gravel")]
        [InlineData(0x8, "Mud")]
        [InlineData(0x10, "Deep mud")]
        [InlineData(0x20, "Dirt")]
        [InlineData(0x40, "Water")]
        [InlineData(0x80, "Rock")]
        [InlineData(0x100, "Loose rock")]
        [InlineData(0x200, "Wood")]
        [InlineData(0x400, "Log")]
        [InlineData(0x800, "Snow")]
        [InlineData(0x1000, "Deep snow")]
        [InlineData(0x2000, "Ice")]
        [InlineData(0x4000, "Sand")]
        [InlineData(0x8000, "Deep sand")]
        [InlineData(0x10000, "Quicksand")]
        [InlineData(0x20000, "Bricks")]
        [InlineData(0x40000, "Light snow")]
        public void SurfaceName_KnownLabel_ReadableName(int label, string name)
        {
            Assert.Equal(name, HudText.SurfaceName(label));
        }

        [Theory]
        [InlineData(HudText.Airborne, "Air")]
        [InlineData(0, "Ground")]
        [InlineData(-5, "Ground")]
        [InlineData(0x1, "Ground")]
        [InlineData(0x80000, "Ground")]
        [InlineData(0x800 | 0x4, "Gravel")]
        public void SurfaceName_NoneUnknownOrSeveral_Fallback(int label, string name)
        {
            Assert.Equal(name, HudText.SurfaceName(label));
        }

        [Fact]
        public void MainSurface_AllSame_OneName()
        {
            Assert.Equal("Snow", Surface(0x800, 0x800, 0x800, 0x800));
        }

        [Fact]
        public void MainSurface_Mixed_MostCommonFirst()
        {
            Assert.Equal("Snow / Gravel", Surface(0x4, 0x800, 0x800, 0x800));
        }

        [Fact]
        public void MainSurface_Tie_WheelOrder()
        {
            Assert.Equal("Mud / Grass", Surface(0x8, 0x2, 0x8, 0x2));
        }

        [Fact]
        public void MainSurface_SomeInAir_IgnoresThem()
        {
            Assert.Equal("Ice", Surface(HudText.Airborne, 0x2000, HudText.Airborne, HudText.Airborne));
        }

        [Fact]
        public void MainSurface_AllInAirOrNoWheels_AirOrEmpty()
        {
            Assert.Equal("Air", Surface(HudText.Airborne, HudText.Airborne));
            Assert.Equal("", Surface());
        }

        [Theory]
        [InlineData(1f, "100%")]
        [InlineData(1.3f, "130%")]
        [InlineData(0.6f, "60%")]
        [InlineData(-1f, "0%")]
        [InlineData(50f, "999%")]
        [InlineData(float.NaN, "—")]
        public void Traction_Percent(float grip, string text)
        {
            Assert.Equal(text, HudText.Traction(grip));
        }

        [Theory]
        [InlineData(1f, 0)]
        [InlineData(1.004f, 0)]
        [InlineData(1.3f, 1)]
        [InlineData(0.85f, -1)]
        [InlineData(float.PositiveInfinity, 0)]
        public void TractionTrend_ComparedWithNormal(float grip, int trend)
        {
            Assert.Equal(trend, HudText.TractionTrend(grip));
        }

        [Theory]
        [InlineData("TopLeft", 0)]
        [InlineData("topright", 1)]
        [InlineData("Bottom Left", 2)]
        [InlineData("bottom-right", 3)]
        [InlineData("BOTTOM_RIGHT", 3)]
        public void TryParseCorner_CornerName_Parsed(string text, int corner)
        {
            Assert.True(HudText.TryParseCorner(text, out var c));
            Assert.Equal((HudCorner)corner, c);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("Middle")]
        [InlineData("2")]
        public void TryParseCorner_Unknown_RejectedTopLeft(string text)
        {
            Assert.False(HudText.TryParseCorner(text, out var c));
            Assert.Equal(HudCorner.TopLeft, c);
        }

        [Theory]
        [InlineData("FL", 0)]
        [InlineData("RR", 2)]
        [InlineData("ML", 1)]
        [InlineData("W3", 1)]
        [InlineData("", 1)]
        [InlineData(null, 1)]
        public void Row_FrontMiddleRear(string label, int row)
        {
            Assert.Equal(row, HudText.Row(label));
        }
    }
}
