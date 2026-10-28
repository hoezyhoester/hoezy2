using System;
using System.Text;

namespace GenericTelemetryProvider
{
    // One packet from Xerenge's telemetry (rexglue/src/telemetry.cpp,
    // docs/telemetry.md), little-endian, 128 bytes:
    //    0 char[4] "XRTM"
    //    4 u16  version (1)
    //    6 u16  flags: 1 in a race, 2 inputs known
    //    8 u32  sequence
    //   16 f64  time, seconds
    //   24 f64  position x, y, z (metres, y up)
    //   48 f64  forward x, y, z
    //   72 f64  up x, y, z
    //   96 f32  speed, m/s
    //  100 f32  throttle, brake, steering, rpm, gear (-1 when not known)
    public struct BurnoutRevengeFrame
    {
        public ushort version;
        public ushort flags;
        public uint sequence;
        public double time;
        public double posX, posY, posZ;
        public double fwdX, fwdY, fwdZ;
        public double upX, upY, upZ;
        public float speed;
        public float throttle, brake, steering, rpm, gear;

        public bool InRace { get { return (flags & BurnoutRevengeAPI.FlagInRace) != 0; } }
        public bool InputsKnown { get { return (flags & BurnoutRevengeAPI.FlagInputs) != 0; } }
    }

    public static class BurnoutRevengeAPI
    {
        public const int DefaultPort = 20790;
        public const int PacketSize = 128;
        public const ushort Version = 1;
        public const ushort FlagInRace = 1;
        public const ushort FlagInputs = 2;

        static readonly byte[] Magic = Encoding.ASCII.GetBytes("XRTM");

        public static bool TryParse(byte[] data, out BurnoutRevengeFrame frame)
        {
            frame = new BurnoutRevengeFrame();
            if (data == null || data.Length < PacketSize)
                return false;
            for (int i = 0; i < 4; ++i)
            {
                if (data[i] != Magic[i])
                    return false;
            }

            // Little-endian on the wire; BitConverter follows the machine, and
            // every machine SpaceMonkey runs on is little-endian.
            frame.version = BitConverter.ToUInt16(data, 4);
            if (frame.version != Version)
                return false;
            frame.flags = BitConverter.ToUInt16(data, 6);
            frame.sequence = BitConverter.ToUInt32(data, 8);
            frame.time = BitConverter.ToDouble(data, 16);
            frame.posX = BitConverter.ToDouble(data, 24);
            frame.posY = BitConverter.ToDouble(data, 32);
            frame.posZ = BitConverter.ToDouble(data, 40);
            frame.fwdX = BitConverter.ToDouble(data, 48);
            frame.fwdY = BitConverter.ToDouble(data, 56);
            frame.fwdZ = BitConverter.ToDouble(data, 64);
            frame.upX = BitConverter.ToDouble(data, 72);
            frame.upY = BitConverter.ToDouble(data, 80);
            frame.upZ = BitConverter.ToDouble(data, 88);
            frame.speed = BitConverter.ToSingle(data, 96);
            frame.throttle = BitConverter.ToSingle(data, 100);
            frame.brake = BitConverter.ToSingle(data, 104);
            frame.steering = BitConverter.ToSingle(data, 108);
            frame.rpm = BitConverter.ToSingle(data, 112);
            frame.gear = BitConverter.ToSingle(data, 116);

            return !double.IsNaN(frame.time) && !double.IsNaN(frame.posX) && !double.IsNaN(frame.fwdX) && !double.IsNaN(frame.upX);
        }
    }
}
