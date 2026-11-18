using System.Collections.Generic;

namespace TireDamage.Tests
{
    public class FieldRepairTests
    {
        private static readonly string[] SparesWords = { "sparewheel" };
        private static readonly string[] Wreath = { "wreath" };
        private static readonly string[] ToolWords = { "pipewrench", "hiliftjack" };
        private static readonly string[] MountedParts =
        {
            "AirHorns", "Sparewheel_Rear", "Sparewheel_Wreath", "Sparewheel_cover_00",
            "Side_Tool_Sledgehammer", "Side_Tool_PipeWrench", "", null,
        };
        private static readonly string[] Keywords = { "jerry", "fuel", "tank" };

        private static List<Tire> Tires(params TireState[] states)
        {
            var list = new List<Tire>();
            foreach (var s in states)
            {
                var t = new Tire();
                t.Force(s, 1f);
                list.Add(t);
            }
            return list;
        }

        [Fact]
        public void Plan_AllGood_NothingToDo()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Ok, TireState.Ok), true, 2);

            Assert.Equal(RepairKind.None, plan.Kind);
            Assert.Equal(-1, plan.Tire);
            Assert.NotNull(plan.Reason);
        }

        [Fact]
        public void Plan_PoppedWithToolKit_PatchesAndKeepsSpares()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Ok, TireState.Popped), true, 1);

            Assert.Equal(RepairKind.Patch, plan.Kind);
            Assert.Equal(1, plan.Tire);
            Assert.Null(plan.Reason);
        }

        [Fact]
        public void Plan_PoppedWithoutToolKit_UsesSpare()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Popped), false, 1);

            Assert.Equal(RepairKind.Spare, plan.Kind);
        }

        [Fact]
        public void Plan_RupturedFirst_EvenWhenAPoppedTireComesFirst()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Popped, TireState.Ruptured), true, 1);

            Assert.Equal(RepairKind.Spare, plan.Kind);
            Assert.Equal(1, plan.Tire);
        }

        [Fact]
        public void Plan_RupturedNoSpareButPoppedAndToolKit_PatchesPopped()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Ruptured, TireState.Popped), true, 0);

            Assert.Equal(RepairKind.Patch, plan.Kind);
            Assert.Equal(1, plan.Tire);
        }

        [Fact]
        public void Plan_RupturedNoSpare_ExplainsNeedForSpare()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Ruptured), true, 0);

            Assert.Equal(RepairKind.None, plan.Kind);
            Assert.Contains("spare", plan.Reason);
        }

        [Fact]
        public void Plan_PoppedNothingAvailable_ExplainsOptions()
        {
            var plan = FieldRepair.Plan(Tires(TireState.Popped), false, 0);

            Assert.Equal(RepairKind.None, plan.Kind);
            Assert.Contains("tool kit", plan.Reason);
        }

        [Fact]
        public void CountParts_SparesExcludingDecorations()
        {
            Assert.Equal(2, FieldRepair.CountParts(MountedParts, SparesWords, Wreath));
        }

        [Fact]
        public void CountParts_ToolKit_CaseInsensitive()
        {
            Assert.Equal(1, FieldRepair.CountParts(MountedParts, ToolWords, null));
        }

        [Fact]
        public void CountParts_NoKeywordsOrParts_Zero()
        {
            Assert.Equal(0, FieldRepair.CountParts(MountedParts, new List<string>(), null));
            Assert.Equal(0, FieldRepair.CountParts(MountedParts, null, null));
            Assert.Equal(0, FieldRepair.CountParts(null, SparesWords, null));
        }

        [Fact]
        public void ParseKeywords_TrimsAndDropsEmpty()
        {
            Assert.Equal(Keywords, FieldRepair.ParseKeywords(" jerry, ,fuel ,tank,"));
            Assert.Empty(FieldRepair.ParseKeywords("  "));
            Assert.Empty(FieldRepair.ParseKeywords(null));
        }

        [Theory]
        [InlineData(-0.8f, 1.4f, "FL")]
        [InlineData(0.8f, 1.4f, "FR")]
        [InlineData(-0.8f, -1.4f, "RL")]
        [InlineData(0.8f, -1.4f, "RR")]
        [InlineData(-0.8f, 0f, "ML")]
        [InlineData(0f, 0f, "MR")]
        public void WheelLabel_Position_ReturnsCorner(float x, float z, string expected)
        {
            Assert.Equal(expected, FieldRepair.WheelLabel(x, z, -1.4f, 1.4f));
        }

        [Theory]
        [InlineData(-1f, "FL")]
        [InlineData(1f, "FR")]
        public void WheelLabel_NoSpread_TreatedAsFront(float x, string expected)
        {
            Assert.Equal(expected, FieldRepair.WheelLabel(x, 0f, 0f, 0f));
            Assert.Equal(expected, FieldRepair.WheelLabel(x, 0f, float.NaN, float.NaN));
        }

        [Theory]
        [InlineData((int)TireState.Ok, 0.724f, "72%")]
        [InlineData((int)TireState.Popped, 0f, "POPPED")]
        [InlineData((int)TireState.Ruptured, 0f, "RUPTURED")]
        public void Describe_State_Text(int state, float health, string expected)
        {
            Assert.Equal(expected, FieldRepair.Describe((TireState)state, health));
        }
    }
}
