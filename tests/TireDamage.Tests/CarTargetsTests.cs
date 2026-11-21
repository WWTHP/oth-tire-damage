using System.Collections.Generic;
using System.IO;

namespace TireDamage.Tests
{
    public class CarTargetsTests
    {
        private static PressureRules Rules() => new()
        {
            NormalPsi = 32f,
            MinPsi = 8f,
            SafePsi = 16f,
            GripAtMin = 1.3f,
            CornerGripAtMin = 0.85f,
            DragAtMin = 0.5f,
            RadiusAtMin = 0.92f,
        };

        private static CarTargets Build(float psi, params TireState[] states) =>
            CarTargets.Build(psi, Rules(), states, 0.6f, 0.35f, 0.85f, 0.75f);

        private static byte[] Encode(CarTargets t)
        {
            var ms = new MemoryStream();
            t.Write(new BinaryWriter(ms));
            return ms.ToArray();
        }

        private static bool Decode(byte[] bytes, out CarTargets t) =>
            CarTargets.TryRead(new BinaryReader(new MemoryStream(bytes)), out t);

        private static byte[] Raw(byte version, byte wheels, float[] head, params float[] perWheel)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(version);
            w.Write(wheels);
            foreach (var f in head) w.Write(f);
            foreach (var f in perWheel) w.Write(f);
            return ms.ToArray();
        }

        private static readonly float[] NeutralHead = { 32f, 1f, 1f, 1f };

        [Fact]
        public void Build_FullPressureGoodTires_Neutral()
        {
            var t = Build(32f, TireState.Ok, TireState.Ok);

            Assert.Equal(1f, t.WheelGrip(0, true, 0f));
            Assert.Equal(1f, t.WheelGrip(1, false, 1f));
            Assert.Equal(1f, t.WheelDrag(0, true));
            Assert.Equal(1f, t.WheelRadius(1));
        }

        [Fact]
        public void Build_LowestPressure_MatchesPressureRules()
        {
            var t = Build(8f, TireState.Ok);

            Assert.Equal(1.3f, t.WheelGrip(0, true, 0f), 4);
            Assert.Equal(1f, t.WheelGrip(0, false, 0f), 4);
            Assert.Equal(0.85f, t.WheelGrip(0, true, 1f), 4);
            Assert.Equal(Pressure.WheelGrip(8f, Rules(), true, 0.4f), t.WheelGrip(0, true, 0.4f), 4);
            Assert.Equal(0.5f, t.WheelDrag(0, true), 4);
            Assert.Equal(1f, t.WheelDrag(0, false));
            Assert.Equal(0.92f, t.WheelRadius(0), 4);
        }

        [Fact]
        public void Build_PressureOutOfRange_Clamped()
        {
            Assert.Equal(8f, Build(2f, TireState.Ok).Psi);
            Assert.Equal(32f, Build(50f, TireState.Ok).Psi);
        }

        [Fact]
        public void Build_FlatTires_FixedGripRadiusAndNoDragChange()
        {
            var t = Build(8f, TireState.Popped, TireState.Ruptured, TireState.Ok);

            Assert.Equal(0.6f, t.WheelGrip(0, true, 0.2f));
            Assert.Equal(0.35f, t.WheelGrip(1, false, 0.9f));
            Assert.Equal(0.85f, t.WheelRadius(0));
            Assert.Equal(0.75f, t.WheelRadius(1));
            Assert.Equal(1f, t.WheelDrag(0, true));
            Assert.Equal(0.5f, t.WheelDrag(2, true), 4);
        }

        [Fact]
        public void Build_BadFlatSettings_Bounded()
        {
            var t = CarTargets.Build(32f, Rules(), new[] { TireState.Popped, TireState.Ruptured }, float.NaN, 9f, 0.1f, float.PositiveInfinity);

            Assert.Equal(1f, t.FlatGrip[0]);
            Assert.Equal(1f, t.FlatGrip[1]);
            Assert.Equal(0.5f, t.RadiusMul[0]);
            Assert.Equal(1f, t.RadiusMul[1]);
        }

        [Fact]
        public void Build_MoreWheelsThanMax_Truncated()
        {
            var states = new List<TireState>();
            for (int i = 0; i < 12; i++) states.Add(TireState.Ok);

            var t = CarTargets.Build(20f, Rules(), states, 0.6f, 0.35f, 0.85f, 0.75f);

            Assert.Equal(CarTargets.MaxWheels, t.Wheels);
        }

        [Fact]
        public void Wheel_IndexOutOfRangeOrBadShare_Neutral()
        {
            var t = Build(8f, TireState.Ok);

            Assert.Equal(1f, t.WheelGrip(-1, true, 0f));
            Assert.Equal(1f, t.WheelGrip(5, true, 0f));
            Assert.Equal(1f, t.WheelDrag(5, true));
            Assert.Equal(1f, t.WheelRadius(-1));
            Assert.Equal(t.WheelGrip(0, true, 0.5f), t.WheelGrip(0, true, float.NaN), 4);
            Assert.Equal(t.WheelGrip(0, true, 1f), t.WheelGrip(0, true, 7f), 4);
        }

        [Fact]
        public void NewTargets_NoWheels_Neutral()
        {
            var t = new CarTargets();

            Assert.Equal(0, t.Wheels);
            Assert.Equal(1f, t.WheelGrip(0, true, 0f));
        }

        [Fact]
        public void SameAs_ComparesEveryValue()
        {
            var a = Build(8f, TireState.Ok, TireState.Popped);

            Assert.True(a.SameAs(Build(8f, TireState.Ok, TireState.Popped)));
            Assert.False(a.SameAs(null));
            Assert.False(a.SameAs(Build(8f, TireState.Ok)));
            Assert.False(a.SameAs(Build(20f, TireState.Ok, TireState.Popped)));
            Assert.False(a.SameAs(Build(8f, TireState.Popped, TireState.Popped)));
            var b = Build(8f, TireState.Ok, TireState.Popped);
            b.Psi = 8.5f;
            Assert.False(a.SameAs(b));
            var c = Build(8f, TireState.Ok, TireState.Popped);
            c.RadiusMul[0] = 0.5f;
            Assert.False(a.SameAs(c));
        }

        [Fact]
        public void Wire_RoundTrip_SameTargets()
        {
            var t = Build(12f, TireState.Ok, TireState.Popped, TireState.Ruptured, TireState.Ok);

            Assert.True(Decode(Encode(t), out var back));

            Assert.True(t.SameAs(back, 0f));
        }

        [Fact]
        public void Wire_NeutralNoWheels_RoundTrips()
        {
            Assert.True(Decode(Encode(new CarTargets()), out var back));

            Assert.Equal(0, back.Wheels);
        }

        [Fact]
        public void Wire_Truncated_Rejected()
        {
            var bytes = Encode(Build(12f, TireState.Ok, TireState.Ok));

            for (int len = 0; len < bytes.Length; len++)
                Assert.False(Decode(bytes[..len], out _), $"length {len}");
        }

        [Theory]
        [InlineData(2, 0)]
        [InlineData(1, 9)]
        public void Wire_BadVersionOrWheelCount_Rejected(byte version, byte wheels)
        {
            Assert.False(Decode(Raw(version, wheels, NeutralHead), out var t));
            Assert.Null(t);
        }

        [Theory]
        [InlineData(float.NaN, 1f, 1f, 1f)]
        [InlineData(-1f, 1f, 1f, 1f)]
        [InlineData(32f, 0.9f, 1f, 1f)]
        [InlineData(32f, 2.5f, 1f, 1f)]
        [InlineData(32f, 1f, 0.1f, 1f)]
        [InlineData(32f, 1f, 1.1f, 1f)]
        [InlineData(32f, 1f, 1f, -0.1f)]
        [InlineData(32f, 1f, 1f, float.PositiveInfinity)]
        public void Wire_HeaderOutOfRange_Rejected(float psi, float loose, float corner, float drag)
        {
            Assert.False(Decode(Raw(1, 0, new[] { psi, loose, corner, drag }), out _));
        }

        [Theory]
        [InlineData(0.01f, 1f)]
        [InlineData(1.5f, 1f)]
        [InlineData(float.NaN, 1f)]
        [InlineData(0f, 0.2f)]
        [InlineData(0f, 1.2f)]
        [InlineData(0f, float.NaN)]
        public void Wire_WheelOutOfRange_Rejected(float flat, float radius)
        {
            Assert.False(Decode(Raw(1, 1, NeutralHead, flat, radius), out _));
        }

        [Fact]
        public void Wire_ValidWheel_Accepted()
        {
            Assert.True(Decode(Raw(1, 1, NeutralHead, 0.6f, 0.85f), out var t));

            Assert.Equal(0.6f, t.WheelGrip(0, true, 0f));
        }
    }

    public class TireWireTests
    {
        private static BinaryReader Reader(params byte[] b) => new(new MemoryStream(b));

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Hello_RoundTrip(bool reply)
        {
            var ms = new MemoryStream();
            TireWire.WriteHello(new BinaryWriter(ms), reply);

            Assert.True(TireWire.TryReadHello(Reader(ms.ToArray()), out bool back));

            Assert.Equal(reply, back);
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 1 })]
        [InlineData(new byte[] { 2, 0 })]
        [InlineData(new byte[] { 1, 2 })]
        public void Hello_Malformed_Rejected(byte[] bytes)
        {
            Assert.False(TireWire.TryReadHello(Reader(bytes), out bool reply));
            Assert.False(reply);
        }

        [Fact]
        public void Applied_RoundTrip_ClampsWheels()
        {
            var ms = new MemoryStream();
            TireWire.WriteApplied(new BinaryWriter(ms), TireWire.ApplyStatus.Applying, 20);

            Assert.True(TireWire.TryReadApplied(Reader(ms.ToArray()), out var status, out int wheels));

            Assert.Equal(TireWire.ApplyStatus.Applying, status);
            Assert.Equal(CarTargets.MaxWheels, wheels);
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 1, 1 })]
        [InlineData(new byte[] { 2, 1, 4 })]
        [InlineData(new byte[] { 1, 3, 4 })]
        [InlineData(new byte[] { 1, 1, 9 })]
        public void Applied_Malformed_Rejected(byte[] bytes)
        {
            Assert.False(TireWire.TryReadApplied(Reader(bytes), out var status, out int wheels));
            Assert.Equal(TireWire.ApplyStatus.NoCar, status);
            Assert.Equal(0, wheels);
        }
    }
}
