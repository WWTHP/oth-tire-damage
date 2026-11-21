using System.IO;

namespace TireDamage
{
    /// <summary>Small network messages besides <see cref="CarTargets"/>: Hello and the host's Applied reply.</summary>
    internal static class TireWire
    {
        public const byte Version = 1;

        /// <summary>What the host does with a player's tires.</summary>
        internal enum ApplyStatus : byte
        {
            /// <summary>The host has no car of this player with its physics running there.</summary>
            NoCar = 0,

            /// <summary>The host applies the player's tires to their car.</summary>
            Applying = 1,

            /// <summary>The host has the tire mod switched off.</summary>
            HostOff = 2,
        }

        public static void WriteHello(BinaryWriter w, bool reply)
        {
            w.Write(Version);
            w.Write(reply ? (byte)1 : (byte)0);
        }

        public static bool TryReadHello(BinaryReader r, out bool reply)
        {
            reply = false;
            try
            {
                if (r.ReadByte() != Version) return false;
                byte b = r.ReadByte();
                if (b > 1) return false;
                reply = b == 1;
                return true;
            }
            catch (EndOfStreamException) { return false; }
        }

        public static void WriteApplied(BinaryWriter w, ApplyStatus status, int wheels)
        {
            w.Write(Version);
            w.Write((byte)status);
            w.Write((byte)System.Math.Clamp(wheels, 0, CarTargets.MaxWheels));
        }

        public static bool TryReadApplied(BinaryReader r, out ApplyStatus status, out int wheels)
        {
            status = ApplyStatus.NoCar;
            wheels = 0;
            try
            {
                if (r.ReadByte() != Version) return false;
                byte s = r.ReadByte();
                byte n = r.ReadByte();
                if (s > (byte)ApplyStatus.HostOff || n > CarTargets.MaxWheels) return false;
                status = (ApplyStatus)s;
                wheels = n;
                return true;
            }
            catch (EndOfStreamException) { return false; }
        }
    }
}
