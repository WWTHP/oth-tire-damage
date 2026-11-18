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
            RadiusAtMin = 0.92f,
            WearPerKmAtMin = 0.5f,
            HitFactorAtMin = 1.5f,
        };

        [Fact]
        public void FullPressure_EveryEffectExactlyNeutral()
        {
            var r = Rules();

            Assert.Equal(1f, Pressure.Grip(32f, r));
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
            r.RadiusAtMin = 0.1f;
            r.WearPerKmAtMin = -3f;
            r.HitFactorAtMin = 0.2f;

            Assert.Equal(1f, Pressure.Grip(8f, r));
            Assert.Equal(0.5f, Pressure.RadiusMul(8f, r));
            Assert.Equal(0f, Pressure.WearPerKm(8f, r));
            Assert.Equal(1f, Pressure.HitFactor(8f, r));
        }

        [Fact]
        public void Describe_FullLowAndTooLow()
        {
            var r = Rules();

            Assert.StartsWith("Full pressure", Pressure.Describe(32f, r));
            Assert.Equal("Grip +15%  ·  Speed -4%", Pressure.Describe(20f, r));
            Assert.EndsWith("TOO LOW: tires wearing", Pressure.Describe(10f, r));
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
