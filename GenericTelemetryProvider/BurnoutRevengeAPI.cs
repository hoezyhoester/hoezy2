using System;
using System.Text;

namespace GenericTelemetryProvider
{
    // One packet from Xerenge's telemetry (rexglue/src/telemetry.cpp,
    // docs/telemetry.md) or DriveClub Recomp's (src/runtime_rig.c), little-endian.
    // Version 1 (Burnout Revenge), 128 bytes:
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
    // Version 2 (DriveClub), 192 bytes: the above, and
    //    6 u16  flags: also 4 the car's own accelerations, 8 suspension per wheel
    //   12 u32  game (2: DriveClub, 3: Star Fox 64)
    //  120 f32  clutch
    //  128 f32  acceleration right, up, forward (m/s^2, up 0 at rest)
    //  140 f32  yaw rate (rad/s, + right)  144 pitch (rad, + nose up)  148 roll  152 yaw (rad)
    //  156 f32  impact (g)
    //  160 f32  suspension travel FL FR RL RR (mm, + compressed)
    //  176 f32  each wheel's load over its average, FL FR RL RR
    // 224 bytes (flag 16): also
    //  192 f32  slip ratio FL FR RL RR (0 rolling, + spinning, - locking)
    //  208 f32  slip angle FL FR RL RR (rad)
    // In version 2 the position is 0: the provider moves the car along
    // its forward direction at its speed.
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
        public uint game;
        public float clutch;
        public float accelRight, accelUp, accelForward;
        public float yawRate, pitch, roll, yaw, impact;
        public float[] suspension;   // FL FR RL RR, mm
        public float[] loadRatio;    // FL FR RL RR
        public float[] slipRatio;    // FL FR RL RR, null when not sent
        public float[] slipAngle;

        public bool InRace { get { return (flags & BurnoutRevengeAPI.FlagInRace) != 0; } }
        public bool InputsKnown { get { return (flags & BurnoutRevengeAPI.FlagInputs) != 0; } }
        public bool HasDynamics { get { return version >= 2 && (flags & BurnoutRevengeAPI.FlagDynamics) != 0; } }
        public bool HasSuspension { get { return version >= 2 && (flags & BurnoutRevengeAPI.FlagSuspension) != 0 && suspension != null; } }
        public bool HasSlip { get { return version >= 2 && (flags & BurnoutRevengeAPI.FlagSlip) != 0 && slipRatio != null; } }
    }

    public static class BurnoutRevengeAPI
    {
        public const int DefaultPort = 20790;
        public const int PacketSize = 128;
        public const int PacketSizeV2 = 192;
        public const ushort Version = 2;
        public const ushort FlagInRace = 1;
        public const ushort FlagInputs = 2;
        public const ushort FlagDynamics = 4;
        public const ushort FlagSuspension = 8;
        public const ushort FlagSlip = 16;
        public const int PacketSizeSlip = 224;
        public const uint GameBurnoutRevenge = 1;
        public const uint GameDriveClub = 2;
        public const uint GameStarFox64 = 3;   // Starfox64Recomp: the player's Arwing, Landmaster or Blue-Marine

        public static string GameName(uint game)
        {
            if (game == GameDriveClub) return "DriveClub";
            if (game == GameStarFox64) return "Star Fox 64";
            return "Burnout Revenge";
        }

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
            if (frame.version < 1 || frame.version > Version)
                return false;
            if (frame.version >= 2 && data.Length < PacketSizeV2)
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
            frame.game = GameBurnoutRevenge;

            if (frame.version >= 2)
            {
                frame.game = BitConverter.ToUInt32(data, 12);
                frame.clutch = BitConverter.ToSingle(data, 120);
                frame.accelRight = BitConverter.ToSingle(data, 128);
                frame.accelUp = BitConverter.ToSingle(data, 132);
                frame.accelForward = BitConverter.ToSingle(data, 136);
                frame.yawRate = BitConverter.ToSingle(data, 140);
                frame.pitch = BitConverter.ToSingle(data, 144);
                frame.roll = BitConverter.ToSingle(data, 148);
                frame.yaw = BitConverter.ToSingle(data, 152);
                frame.impact = BitConverter.ToSingle(data, 156);
                frame.suspension = new float[4];
                frame.loadRatio = new float[4];
                for (int i = 0; i < 4; ++i)
                {
                    frame.suspension[i] = BitConverter.ToSingle(data, 160 + 4 * i);
                    frame.loadRatio[i] = BitConverter.ToSingle(data, 176 + 4 * i);
                }
                if (data.Length >= PacketSizeSlip)
                {
                    frame.slipRatio = new float[4];
                    frame.slipAngle = new float[4];
                    for (int i = 0; i < 4; ++i)
                    {
                        frame.slipRatio[i] = BitConverter.ToSingle(data, 192 + 4 * i);
                        frame.slipAngle[i] = BitConverter.ToSingle(data, 208 + 4 * i);
                    }
                }
                if (float.IsNaN(frame.accelRight) || float.IsNaN(frame.accelUp) || float.IsNaN(frame.accelForward))
                    return false;
            }

            return !double.IsNaN(frame.time) && !double.IsNaN(frame.posX) && !double.IsNaN(frame.fwdX) && !double.IsNaN(frame.upX);
        }
    }
}
