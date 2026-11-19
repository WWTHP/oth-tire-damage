namespace TireDamage.Tests
{
    public class PressureTests
    {
        private static PressureRules Rules() => new()
        {
            NormalPsi = 32f,
            MinPsi = 8f,
            SafePsi = 16f,
            GripAtMin = 1.3f,
            CornerGripAtMin = 0.85f,
            RadiusAtMin = 0.92f,
            WearPerKmAtMin = 0.5f,
            HitFactorAtMin = 1.5f,
        };

        [Fact]
        public void FullPressure_EveryEffectExactlyNeutral()
        {
            var r = Rules();

            Assert.Equal(1f, Pressure.Grip(32f, r));
            Assert.Equal(1f, Pressure.CornerGrip(32f, r));
            Assert.Equal(1f, Pressure.WheelGrip(32f, r, true, 0.3f));
            Assert.Equal(1f, Pressure.RadiusMul(32f, r));
            Assert.Equal(0f, Pressure.WearPerKm(32f, r));
            Assert.Equal(1f, Pressure.HitFactor(32f, r));
            Assert.Equal(0f, Pressure.Softness(32f, r));
        }

        [Fact]
        public void MinPressure_FullEffects()
        {
            var r = Rules();

            Assert.Equal(1.3f, Pressure.Grip(8f, r), 4);
            Assert.Equal(0.85f, Pressure.CornerGrip(8f, r), 4);
            Assert.Equal(0.92f, Pressure.RadiusMul(8f, r), 4);
            Assert.Equal(0.5f, Pressure.WearPerKm(8f, r), 4);
            Assert.Equal(1.5f, Pressure.HitFactor(8f, r), 4);
        }

        [Fact]
        public void HalfwayPressure_LinearGripAndRadius()
        {
            var r = Rules();

            Assert.Equal(1.15f, Pressure.Grip(20f, r), 4);
            Assert.Equal(0.96f, Pressure.RadiusMul(20f, r), 4);
        }

        [Theory]
        [InlineData(16f, 0f)]
        [InlineData(20f, 0f)]
        [InlineData(12f, 0.5f)]
        [InlineData(8f, 1f)]
        public void Danger_OnlyBelowSafe(float psi, float expected)
        {
            Assert.Equal(expected, Pressure.Danger(psi, Rules()), 4);
        }

        [Theory]
        [InlineData(40f, 32f)]
        [InlineData(2f, 8f)]
        [InlineData(float.NaN, 32f)]
        [InlineData(float.PositiveInfinity, 32f)]
        [InlineData(20.4f, 20.4f)]
        public void Clamp_KeepsPsiInRange(float psi, float expected)
        {
            Assert.Equal(expected, Pressure.Clamp(psi, Rules()), 4);
        }

        [Theory]
        [InlineData(20f, 1, 21f)]
        [InlineData(20.4f, -1, 19f)]
        [InlineData(32f, 3, 32f)]
        [InlineData(8f, -5, 8f)]
        public void Change_StepsWholePsiWithinRange(float psi, int steps, float expected)
        {
            Assert.Equal(expected, Pressure.Change(psi, steps, Rules()), 4);
        }

        [Fact]
        public void Bounds_InvertedOrNonFiniteRules_Sanitised()
        {
            var r = new PressureRules { NormalPsi = 10f, MinPsi = 20f, SafePsi = 50f };

            Pressure.Bounds(r, out var min, out var normal, out var safe);

            Assert.Equal(10f, normal);
            Assert.Equal(9f, min);
            Assert.Equal(10f, safe);

            var bad = new PressureRules { NormalPsi = float.NaN, MinPsi = float.NaN, SafePsi = float.NaN };
            Pressure.Bounds(bad, out min, out normal, out safe);
            Assert.Equal(32f, normal);
            Assert.Equal(8f, min);
            Assert.Equal(16f, safe);

            Pressure.Bounds(new PressureRules { NormalPsi = 0f }, out min, out normal, out _);
            Assert.Equal(2f, normal);
            Assert.Equal(1f, min);

            Pressure.Bounds(null, out _, out normal, out _);
            Assert.Equal(32f, normal);
        }

        [Fact]
        public void Danger_SafeAtMin_NeverDangerous()
        {
            var r = Rules();
            r.SafePsi = 8f;

            Assert.Equal(0f, Pressure.Danger(8f, r));
        }

        [Fact]
        public void Effects_BadTunables_StayNeutralOrSane()
        {
            var r = Rules();
            r.GripAtMin = float.NaN;
            r.CornerGripAtMin = 0.05f;
            r.RadiusAtMin = 0.1f;
            r.WearPerKmAtMin = -3f;
            r.HitFactorAtMin = 0.2f;

            Assert.Equal(1f, Pressure.Grip(8f, r));
            Assert.Equal(0.3f, Pressure.CornerGrip(8f, r), 4);
            Assert.Equal(0.5f, Pressure.RadiusMul(8f, r));
            Assert.Equal(0f, Pressure.WearPerKm(8f, r));
            Assert.Equal(1f, Pressure.HitFactor(8f, r));
        }

        [Fact]
        public void LowPressure_LooseGround_NoWearNoExtraHitDamage()
        {
            var r = Rules();

            Assert.Equal(0f, Pressure.WearPerKm(8f, r, loose: true));
            Assert.Equal(1f, Pressure.HitFactor(8f, r, loose: true));
            Assert.Equal(0.5f, Pressure.WearPerKm(8f, r, loose: false), 4);
            Assert.Equal(1.5f, Pressure.HitFactor(8f, r, loose: false), 4);
        }

        [Fact]
        public void Grip_BelowOneOrCornerAboveOne_Clamped()
        {
            var r = Rules();
            r.GripAtMin = 0.5f;
            r.CornerGripAtMin = 1.4f;

            Assert.Equal(1f, Pressure.Grip(8f, r));
            Assert.Equal(1f, Pressure.CornerGrip(8f, r));
        }

        [Theory]
        [InlineData(0x4000, true)]   // Sand
        [InlineData(0x1000, true)]   // DeepSnow
        [InlineData(0x2000, true)]   // Ice
        [InlineData(0x40000, true)]  // ShallowSnow
        [InlineData(0x2024, true)]   // Grass|Dirt|Ice: any loose flag counts
        [InlineData(0x4, false)]     // Gravel
        [InlineData(0x80, false)]    // Rock
        [InlineData(0x8, true)]      // Mud
        [InlineData(0x10, true)]     // DeepMud
        [InlineData(0, false)]       // None
        public void IsLoose_MudSandSnowIceOnly(int label, bool expected)
        {
            Assert.Equal(expected, Pressure.IsLoose(label));
        }

        [Theory]
        [InlineData(0.1f, 0.2f, 0f, 0.1f, 1f)]      // pure cornering
        [InlineData(0f, 0.2f, 0.05f, 0.1f, 0f)]     // pure wheelspin / braking
        [InlineData(0.1f, 0.2f, 0.05f, 0.1f, 0.5f)] // equal share of each peak
        [InlineData(0.001f, 0.2f, 0.001f, 0.1f, 0.5f)] // hardly slipping: idle
        [InlineData(0.1f, 0f, 0.05f, 0.1f, 0.5f)]   // unknown peak: idle
        [InlineData(float.NaN, 0.2f, 0.05f, 0.1f, 0.5f)]
        public void LateralShare_ComparesSlipsByTheirPeaks(float angle, float peakAngle, float ratio, float peakRatio, float expected)
        {
            Assert.Equal(expected, Pressure.LateralShare(angle, peakAngle, ratio, peakRatio), 4);
        }

        [Fact]
        public void LateralShare_NegativeSlips_UseMagnitude()
        {
            Assert.Equal(1f, Pressure.LateralShare(-0.1f, 0.2f, 0f, 0.1f), 4);
        }

        [Theory]
        [InlineData(true, 0f, 1.3f)]    // loose, straight: full traction bonus
        [InlineData(false, 0f, 1f)]     // hard ground, straight: unchanged
        [InlineData(true, 1f, 0.85f)]   // cornering: worse anywhere
        [InlineData(false, 1f, 0.85f)]
        [InlineData(false, 0.5f, 0.925f)]
        [InlineData(true, float.NaN, 1.075f)]
        public void WheelGrip_TractionOnLooseGroundOnly_CorneringWorse(bool loose, float share, float expected)
        {
            Assert.Equal(expected, Pressure.WheelGrip(8f, Rules(), loose, share), 4);
        }

        [Fact]
        public void Describe_FullLowAndTooLow()
        {
            var r = Rules();

            Assert.StartsWith("Full pressure", Pressure.Describe(32f, r));
            Assert.Equal("Loose ground +20%  ·  Corners -10%  ·  Speed -5%", Pressure.Describe(16f, r));
            Assert.EndsWith("TOO LOW: wears on hard ground", Pressure.Describe(10f, r));
        }

        [Fact]
        public void Abrade_GoodTire_LosesHealthThenPops()
        {
            var t = new Tire();

            Assert.Null(t.Abrade(0.6f));
            Assert.Equal(0.4f, t.Health, 4);
            Assert.Equal(TireState.Popped, t.Abrade(0.5f));
            Assert.Equal(1f, t.Carcass);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void Abrade_InvalidAmount_Ignored(float amount)
        {
            var t = new Tire();

            Assert.Null(t.Abrade(amount));
            Assert.Equal(1f, t.Health);
        }

        [Fact]
        public void Abrade_PoppedTire_Ignored()
        {
            var t = new Tire();
            t.Force(TireState.Popped);

            Assert.Null(t.Abrade(0.5f));
            Assert.Equal(TireState.Popped, t.State);
        }
    }
}
