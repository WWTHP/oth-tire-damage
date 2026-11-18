namespace TireDamage.Tests
{
    public class PadInputTests
    {
        private static readonly string[] Four = { "FL", "FR", "RL", "RR" };
        private static readonly string[] Six = { "FL", "FR", "ML", "MR", "RL", "RR" };

        [Fact]
        public void ParseChord_L3R3_TwoStickPresses()
        {
            var chord = PadInput.ParseChord(" L3 + r3 ");

            Assert.Equal(2, chord.Count);
            Assert.Equal(PadButton.LeftStickPress, chord[0]);
            Assert.Equal(PadButton.RightStickPress, chord[1]);
        }

        [Theory]
        [InlineData("leftStickPress+rightStickPress")]
        [InlineData("LB+RB+A")]
        [InlineData("View")]
        [InlineData("DpadDown")]
        public void ParseChord_KnownNames_Accepted(string text)
        {
            Assert.NotNull(PadInput.ParseChord(text));
            Assert.NotEmpty(PadInput.ParseChord(text));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        [InlineData("none")]
        public void ParseChord_EmptyOrNone_NoChord(string text)
        {
            Assert.Empty(PadInput.ParseChord(text));
        }

        [Theory]
        [InlineData("L3+Banana")]
        [InlineData("L3+L3")]
        [InlineData("L3+")]
        [InlineData("A+B+X+Y")]
        public void ParseChord_UnknownRepeatedOrTooMany_Rejected(string text)
        {
            Assert.Null(PadInput.ParseChord(text));
        }

        [Theory]
        [InlineData(0, (int)NavDir.Right, 1)]
        [InlineData(1, (int)NavDir.Left, 0)]
        [InlineData(0, (int)NavDir.Down, 2)]
        [InlineData(3, (int)NavDir.Up, 1)]
        [InlineData(0, (int)NavDir.Left, 0)]
        [InlineData(0, (int)NavDir.Up, 0)]
        [InlineData(3, (int)NavDir.Down, 3)]
        public void Move_FourWheels_FollowsCarLayout(int from, int dir, int expected)
        {
            Assert.Equal(expected, PadInput.Move(Four, from, (NavDir)dir));
        }

        [Theory]
        [InlineData(0, (int)NavDir.Down, 2)]
        [InlineData(2, (int)NavDir.Down, 4)]
        [InlineData(4, (int)NavDir.Up, 2)]
        [InlineData(3, (int)NavDir.Left, 2)]
        public void Move_SixWheels_StepsOneAxle(int from, int dir, int expected)
        {
            Assert.Equal(expected, PadInput.Move(Six, from, (NavDir)dir));
        }

        [Fact]
        public void Move_NoSelectionOrNoTiles_Defaults()
        {
            Assert.Equal(0, PadInput.Move(Four, -1, NavDir.Up));
            Assert.Equal(0, PadInput.Move(Four, 9, NavDir.Up));
            Assert.Equal(-1, PadInput.Move(System.Array.Empty<string>(), 0, NavDir.Up));
            Assert.Equal(-1, PadInput.Move(null, 0, NavDir.Up));
        }

        [Fact]
        public void Move_OddLabels_TreatedAsRearRight()
        {
            var labels = new[] { "FL", "", null };

            // "" and null both count as rear-right: nothing ahead of them in that column, nothing
            // beside them on the left, and the front-left tile has no right-hand neighbour.
            Assert.Equal(1, PadInput.Move(labels, 1, NavDir.Up));
            Assert.Equal(1, PadInput.Move(labels, 1, NavDir.Left));
            Assert.Equal(0, PadInput.Move(labels, 0, NavDir.Right));
        }
    }
}
