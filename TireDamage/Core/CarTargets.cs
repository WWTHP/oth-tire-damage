using System;
using System.Collections.Generic;
using System.IO;

namespace TireDamage
{
    /// <summary>
    /// What a car's tires should do to its physics, worked out by the car's driver from their own
    /// settings and tire states. The machine that runs the car's physics applies it: the driver's own
    /// machine in single player, the host for every car in multiplayer (the driver sends it there).
    /// </summary>
    internal sealed class CarTargets
    {
        public const byte WireVersion = 1;
        public const int MaxWheels = 8;

        public float Psi = 32f;
        public float LooseGrip = 1f;   // traction on mud/sand/snow/ice (x normal)
        public float CornerGrip = 1f;  // grip when the tire slides sideways (x normal)
        public float DragMul = 1f;     // ground drag on mud/sand/snow/ice (x normal)

        /// <summary>Per wheel: fixed grip of a flat tire, or 0 for a good tire (pressure rules).</summary>
        public float[] FlatGrip = Array.Empty<float>();

        /// <summary>Per wheel: wheel radius (x normal).</summary>
        public float[] RadiusMul = Array.Empty<float>();

        public int Wheels => FlatGrip.Length;

        /// <summary>Targets for the local car from the driver's settings.</summary>
        public static CarTargets Build(float psi, PressureRules r, IReadOnlyList<TireState> states,
                                       float poppedGrip, float rupturedGrip, float poppedRadius, float rupturedRadius)
        {
            psi = Pressure.Clamp(psi, r);
            int n = Math.Min(states.Count, MaxWheels);
            var t = new CarTargets
            {
                Psi = psi,
                LooseGrip = Pressure.Grip(psi, r),
                CornerGrip = Pressure.CornerGrip(psi, r),
                DragMul = Pressure.DragMul(psi, r),
                FlatGrip = new float[n],
                RadiusMul = new float[n],
            };
            float radius = Pressure.RadiusMul(psi, r);
            for (int i = 0; i < n; i++)
            {
                switch (states[i])
                {
                    case TireState.Popped:
                        t.FlatGrip[i] = Bound(poppedGrip, 0.05f, 1f);
                        t.RadiusMul[i] = Bound(poppedRadius, 0.5f, 1f);
                        break;
                    case TireState.Ruptured:
                        t.FlatGrip[i] = Bound(rupturedGrip, 0.05f, 1f);
                        t.RadiusMul[i] = Bound(rupturedRadius, 0.5f, 1f);
                        break;
                    default:
                        t.RadiusMul[i] = radius;
                        break;
                }
            }
            return t;
        }

        /// <summary>
        /// Grip of wheel <paramref name="i"/>: a flat tire's fixed value, else traction on loose ground
        /// blended with cornering grip by how much of the slip is sideways.
        /// </summary>
        public float WheelGrip(int i, bool loose, float lateralShare)
        {
            if (i < 0 || i >= Wheels) return 1f;
            if (FlatGrip[i] > 0f) return FlatGrip[i];
            float straight = loose ? LooseGrip : 1f;
            float s = Math.Clamp(float.IsFinite(lateralShare) ? lateralShare : 0.5f, 0f, 1f);
            return straight + (CornerGrip - straight) * s;
        }

        /// <summary>Ground drag factor of wheel <paramref name="i"/> (a flat tire digs in: unchanged).</summary>
        public float WheelDrag(int i, bool loose)
        {
            if (i < 0 || i >= Wheels || FlatGrip[i] > 0f || !loose) return 1f;
            return DragMul;
        }

        public float WheelRadius(int i) => i >= 0 && i < Wheels ? RadiusMul[i] : 1f;

        /// <summary>True when every value is within <paramref name="eps"/> of <paramref name="o"/>.</summary>
        public bool SameAs(CarTargets o, float eps = 0.002f)
        {
            if (o == null || o.Wheels != Wheels) return false;
            if (Math.Abs(Psi - o.Psi) > 0.05f || Math.Abs(LooseGrip - o.LooseGrip) > eps ||
                Math.Abs(CornerGrip - o.CornerGrip) > eps || Math.Abs(DragMul - o.DragMul) > eps) return false;
            for (int i = 0; i < Wheels; i++)
                if (Math.Abs(FlatGrip[i] - o.FlatGrip[i]) > eps || Math.Abs(RadiusMul[i] - o.RadiusMul[i]) > eps) return false;
            return true;
        }

        // ------------------------------------------------------------------ wire format

        public void Write(BinaryWriter w)
        {
            w.Write(WireVersion);
            w.Write((byte)Wheels);
            w.Write(Psi);
            w.Write(LooseGrip);
            w.Write(CornerGrip);
            w.Write(DragMul);
            for (int i = 0; i < Wheels; i++)
            {
                w.Write(FlatGrip[i]);
                w.Write(RadiusMul[i]);
            }
        }

        /// <summary>Reads targets sent by another player. Rejects anything out of the ranges our settings allow.</summary>
        public static bool TryRead(BinaryReader r, out CarTargets t)
        {
            t = null;
            try
            {
                if (r.ReadByte() != WireVersion) return false;
                int n = r.ReadByte();
                if (n > MaxWheels) return false;
                var c = new CarTargets
                {
                    Psi = r.ReadSingle(),
                    LooseGrip = r.ReadSingle(),
                    CornerGrip = r.ReadSingle(),
                    DragMul = r.ReadSingle(),
                    FlatGrip = new float[n],
                    RadiusMul = new float[n],
                };
                if (!In(c.Psi, 0f, 100f) || !In(c.LooseGrip, 1f, 2f) || !In(c.CornerGrip, 0.3f, 1f) || !In(c.DragMul, 0f, 1f))
                    return false;
                for (int i = 0; i < n; i++)
                {
                    c.FlatGrip[i] = r.ReadSingle();
                    c.RadiusMul[i] = r.ReadSingle();
                    if (!(c.FlatGrip[i] == 0f || In(c.FlatGrip[i], 0.05f, 1f)) || !In(c.RadiusMul[i], 0.5f, 1f)) return false;
                }
                t = c;
                return true;
            }
            catch (EndOfStreamException) { return false; }
        }

        private static bool In(float v, float min, float max) => float.IsFinite(v) && v >= min && v <= max;

        private static float Bound(float v, float min, float max) => float.IsFinite(v) ? Math.Clamp(v, min, max) : max;
    }
}
