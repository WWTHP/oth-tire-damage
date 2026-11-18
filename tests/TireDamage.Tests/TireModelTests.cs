namespace TireDamage.Tests
{
    public class TireModelTests
    {
        private static TireRules Rules() => new()
        {
            HitThreshold = 4f,
            DamageScale = 0.04f,
            RuptureHit = 0.9f,
            FlatWearPerKm = 0.5f,
            FlatHitFactor = 2f,
            PatchHealth = 0.5f,
        };

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(4f, 0f)]
        [InlineData(-3.9f, 0f)]
        [InlineData(6f, 0.16f)]
        [InlineData(-6f, 0.16f)]
        [InlineData(9f, 1f)]
        public void ImpactDamage_Speed_QuadraticAboveThreshold(float speed, float expected)
        {
            Assert.Equal(expected, Tire.ImpactDamage(speed, Rules()), 4);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void ImpactDamage_NonFinite_IsZero(float speed)
        {
            Assert.Equal(0f, Tire.ImpactDamage(speed, Rules()));
        }

        [Fact]
        public void ImpactDamage_NoRules_IsZero()
        {
            Assert.Equal(0f, Tire.ImpactDamage(10f, null));
        }

        [Fact]
        public void Hit_SmallHits_LowerHealthUntilPopped()
        {
            var t = new Tire();
            var r = Rules();

            Assert.Null(t.Hit(0.4f, r));
            Assert.Null(t.Hit(0.4f, r));
            Assert.Equal(0.2f, t.Health, 4);
            Assert.Equal(TireState.Popped, t.Hit(0.4f, r));

            Assert.Equal(TireState.Popped, t.State);
            Assert.Equal(0f, t.Health);
            Assert.Equal(1f, t.Carcass);
        }

        [Fact]
        public void Hit_BigHitThatEmptiesTire_Ruptures()
        {
            var t = new Tire();

            Assert.Equal(TireState.Ruptured, t.Hit(1.2f, Rules()));
            Assert.Equal(0f, t.Carcass);
        }

        [Fact]
        public void Hit_BigHitOnHealthyTireNotEmptying_OnlyDamages()
        {
            var r = Rules();
            r.RuptureHit = 0.5f;
            var t = new Tire();

            Assert.Null(t.Hit(0.6f, r));
            Assert.Equal(TireState.Ok, t.State);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Hit_InvalidDamage_Ignored(float damage)
        {
            var t = new Tire();

            Assert.Null(t.Hit(damage, Rules()));
            Assert.Equal(1f, t.Health);
        }

        [Fact]
        public void Hit_PoppedTire_WearsCarcassByFactorUntilRuptured()
        {
            var t = new Tire();
            var r = Rules();
            t.Force(TireState.Popped);

            Assert.Null(t.Hit(0.2f, r));
            Assert.Equal(0.6f, t.Carcass, 4);
            Assert.Equal(TireState.Ruptured, t.Hit(0.35f, r));
            Assert.Null(t.Hit(1f, r)); // already ruptured
        }

        [Fact]
        public void Drive_OnPoppedTire_RupturesAfterTwoKmAtDefaultWear()
        {
            var t = new Tire();
            var r = Rules();
            t.Force(TireState.Popped);

            Assert.Null(t.Drive(1999f, r));
            Assert.Equal(TireState.Ruptured, t.Drive(2f, r));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-5f)]
        [InlineData(float.NaN)]
        public void Drive_InvalidDistance_Ignored(float metres)
        {
            var t = new Tire();
            t.Force(TireState.Popped);

            Assert.Null(t.Drive(metres, Rules()));
            Assert.Equal(1f, t.Carcass);
        }

        [Fact]
        public void Drive_HealthyTire_NoWear()
        {
            var t = new Tire();

            Assert.Null(t.Drive(100000f, Rules()));
            Assert.Equal(1f, t.Carcass);
        }

        [Fact]
        public void Patch_PoppedTire_BackToPatchHealth()
        {
            var t = new Tire();
            t.Force(TireState.Popped);

            Assert.True(t.Patch(Rules()));
            Assert.Equal(TireState.Ok, t.State);
            Assert.Equal(0.5f, t.Health);
        }

        [Theory]
        [InlineData((int)TireState.Ok)]
        [InlineData((int)TireState.Ruptured)]
        public void Patch_NotPopped_Refused(int stateValue)
        {
            var state = (TireState)stateValue;
            var t = new Tire();
            t.Force(state, 0.3f);

            Assert.False(t.Patch(Rules()));
            Assert.Equal(state, t.State);
        }

        [Fact]
        public void Patch_OutOfRangePatchHealth_Clamped()
        {
            var r = Rules();
            r.PatchHealth = 5f;
            var t = new Tire();
            t.Force(TireState.Popped);

            t.Patch(r);

            Assert.Equal(1f, t.Health);
        }

        [Fact]
        public void Replace_RupturedTire_New()
        {
            var t = new Tire();
            t.Force(TireState.Ruptured);

            t.Replace();

            Assert.Equal(TireState.Ok, t.State);
            Assert.Equal(1f, t.Health);
            Assert.Equal(1f, t.Carcass);
        }

        [Fact]
        public void Force_Ok_ClampsHealth()
        {
            var t = new Tire();

            t.Force(TireState.Ok, 3f);

            Assert.Equal(1f, t.Health);
        }
    }
}
