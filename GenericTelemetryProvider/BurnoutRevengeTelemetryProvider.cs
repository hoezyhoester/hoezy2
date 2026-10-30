using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using Newtonsoft.Json;

namespace GenericTelemetryProvider
{
    // Burnout Revenge, through Xerenge (https://github.com/shipa-2/Xerenge), the
    // native port of the Xbox 360 game. Xerenge sends the player's transform in a
    // race as UDP packets ("XRTM", see BurnoutRevengeAPI); everything else -
    // velocities, accelerations, angular rates - is worked out here, as for the
    // other transform-only games.
    //
    // DriveClub, through DriveClub Recomp, sends version 2 of the same packet: its
    // accelerations come from the game's own simulation (the tyres' forces and
    // loads, the change of speed), and each wheel's suspension from its tyre's
    // load - so bumps and kerbs come through per wheel. Those replace what would
    // be worked out from the transform; the transform (from its yaw, pitch and
    // speed) still gives the angles and angular rates, so the signs are the same
    // as every other game's here.
    class BurnoutRevengeTelemetryProvider : GenericProviderBase
    {
        Thread t;
        public BurnoutRevengeUI ui;
        public int port = BurnoutRevengeAPI.DefaultPort;
        public string gameName = "Burnout Revenge";
        Vector3 simPosition = Vector3.Zero;   // version 2: the car moved along at its speed

        UdpClient socket;
        BurnoutRevengeFrame frame;
        BurnoutRevengeFrame lastRaceFrame;
        bool haveRaceFrame = false;
        double lastFrameTime = 0.0;
        uint lastSequence = 0;
        bool pausedByGame = false;
        string status = "";

        public override void Run()
        {
            base.Run();

            maxAccel2DMagSusp = 6.0f;
            telemetryPausedTime = 1.5f;
            // Burnout's boost runs past 300 km/h; a cut (crash camera, restart)
            // is far faster than that.
            maxPosVelocityDelta = 150.0f;

            t = new Thread(ReadTelemetry);
            t.IsBackground = true;
            t.Start();
        }

        public override void Stop()
        {
            base.Stop();
            CloseSocket();
        }

        public override void StopAllThreads()
        {
            base.StopAllThreads();
            CloseSocket();
        }

        void CloseSocket()
        {
            try
            {
                if (socket != null)
                    socket.Close();
            }
            catch (Exception) { }
            socket = null;
        }

        void SetStatus(string text)
        {
            if (text == status)
                return;
            status = text;
            if (ui != null)
                ui.StatusTextChanged(text);
        }

        void ReadTelemetry()
        {
            StartSending();

            try
            {
                socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                socket.Client.ReceiveTimeout = 100;
            }
            catch (Exception e)
            {
                SetStatus("Cannot listen on UDP port " + port + ": " + e.Message + " (is another SpaceMonkey window using it?)");
                StopSending();
                return;
            }

            SetStatus("Waiting for " + gameName + " on UDP port " + port);

            Stopwatch idle = new Stopwatch();
            idle.Start();
            Stopwatch heartbeat = new Stopwatch();
            heartbeat.Start();

            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);

            while (!IsStopped)
            {
                byte[] data = null;
                try
                {
                    data = socket.Receive(ref from);
                }
                catch (SocketException)
                {
                    // Timed out: nothing this tenth of a second.
                }
                catch (Exception)
                {
                    if (IsStopped)
                        break;
                    Thread.Sleep(100);
                    continue;
                }

                if (data != null && BurnoutRevengeAPI.TryParse(data, out frame))
                {
                    idle.Restart();

                    // The game started again: its clock and count start again too.
                    if (frame.sequence < lastSequence || frame.time < lastFrameTime)
                    {
                        lastFrameTime = 0.0;
                        lastFrameValid = false;
                        simPosition = Vector3.Zero;
                    }
                    lastSequence = frame.sequence;

                    if (frame.InRace)
                    {
                        SetStatus("Receiving telemetry from " + from.Address);
                        if (pausedByGame)
                        {
                            pausedByGame = false;
                            lastFrameValid = false; // start afresh from where the car is now
                            SetTelemetryPaused(false);
                        }

                        if (lastFrameTime == 0.0)
                        {
                            lastFrameTime = frame.time;
                            continue;
                        }

                        double calcDT = frame.time - lastFrameTime;
                        if (calcDT <= 0.0)
                            continue;
                        lastFrameTime = frame.time;

                        lastRaceFrame = frame;
                        haveRaceFrame = true;
                        ProcessFrameData(frame, (float)calcDT);
                        heartbeat.Restart();
                        continue;
                    }

                    SetStatus("Connected to " + from.Address + " - not in a race");
                }
                else if (idle.Elapsed.TotalSeconds > 2.0)
                {
                    SetStatus("Waiting for " + gameName + " on UDP port " + port);
                }

                // Not in a race, or the game has gone quiet: ease the rig back to
                // rest from the last thing it was doing, at about 60 Hz.
                if (haveRaceFrame && (!frame.InRace || idle.Elapsed.TotalSeconds > 0.5))
                {
                    if (!pausedByGame)
                    {
                        pausedByGame = true;
                        SetTelemetryPaused(true);
                    }
                    float sinceLast = (float)heartbeat.Elapsed.TotalSeconds;
                    if (sinceLast >= 1.0f / 60.0f)
                    {
                        heartbeat.Restart();
                        ProcessFrameData(lastRaceFrame, Math.Min(sinceLast, 0.1f));
                    }
                }
            }

            CloseSocket();
            StopSending();
        }

        void ProcessFrameData(BurnoutRevengeFrame f, float _dt)
        {
            fwd = Vector3.Normalize(new Vector3((float)f.fwdX, (float)f.fwdY, (float)f.fwdZ));
            up = Vector3.Normalize(new Vector3((float)f.upX, (float)f.upY, (float)f.upZ));
            rht = Vector3.Cross(up, fwd);

            Matrix4x4 m = new Matrix4x4();
            m.M11 = rht.X; m.M12 = rht.Y; m.M13 = rht.Z; m.M14 = 0.0f;
            m.M21 = up.X; m.M22 = up.Y; m.M23 = up.Z; m.M24 = 0.0f;
            m.M31 = fwd.X; m.M32 = fwd.Y; m.M33 = fwd.Z; m.M34 = 0.0f;
            if (f.version >= 2 && f.posX == 0.0 && f.posY == 0.0 && f.posZ == 0.0)
            {
                // No position from the game: move along the forward direction at its speed.
                simPosition += fwd * f.speed * _dt;
                m.M41 = simPosition.X; m.M42 = simPosition.Y; m.M43 = simPosition.Z;
            }
            else
            {
                m.M41 = (float)f.posX; m.M42 = (float)f.posY; m.M43 = (float)f.posZ;
            }
            m.M44 = 1.0f;

            transform = m;
            ProcessTransform(m, _dt);
        }

        public override bool ProcessTransform(Matrix4x4 newTransform, float inDT)
        {
            if (!base.ProcessTransform(newTransform, inDT))
                return false;

            if (ui != null)
            {
                // The game's own values first: the box jumps back to the top every frame.
                bool flight = frame.game == BurnoutRevengeAPI.GameStarFox64;
                string head = BurnoutRevengeAPI.GameName(frame.game) +
                    (flight ? (frame.InRace ? " - flying" : " - not flying (menus, map, cutscene or paused)")
                            : (frame.InRace ? " - in a race" : " - not in a race")) +
                    "\n speed: " + (frame.speed * 3.6f).ToString("F0") + " km/h" +
                    (flight ? "\n boost: " + (frame.throttle > 0.5f ? "on" : "off") :
                    "\n engine: " + (frame.rpm >= 0.0f ? frame.rpm.ToString("F0") + " rpm" : "rpm not found yet") +
                    ", gear " + (frame.gear >= 0.0f ? frame.gear.ToString("F0") : "not found yet"));
                if (frame.HasDynamics)
                {
                    head += "\n\n FROM THE GAME (m/s2, 9.8 = 1 g)" +
                        "\n  up/down:     " + frame.accelUp.ToString("F1") + "   (crest: negative, dip: positive)" +
                        "\n  right/left:  " + frame.accelRight.ToString("F1") + "   (right-hander: positive)" +
                        "\n  fwd/back:    " + frame.accelForward.ToString("F1") + "   (braking: negative)" +
                        "\n  " + (flight ? "nose pitch:  " : "slope:       ") + (frame.pitch * 57.2958f).ToString("F1") + " deg   (" + (flight ? "climbing" : "uphill") + ": positive)" +
                        "\n  yaw rate:    " + frame.yawRate.ToString("F2") + "   (turning right: positive)" +
                        "\n  impact:      " + frame.impact.ToString("F1") + " g";
                    if (frame.HasSuspension)
                        head += "\n  suspension mm FL/FR/RL/RR: " + string.Join(" / ", Array.ConvertAll(frame.suspension, x => x.ToString("F0")));
                    if (frame.HasSlip)
                        head += "\n  wheel slip FL/FR/RL/RR:    " + string.Join(" / ", Array.ConvertAll(frame.slipRatio, x => x.ToString("F2"))) + "   (spinning: +, locking: -)";
                }
                head += "\n\n TO THE MOTION SOFTWARE (g)" +
                    "\n  vertical:     " + ((float)filteredData.gforce_vertical).ToString("F2") +
                    "\n  lateral:      " + ((float)filteredData.gforce_lateral).ToString("F2") +
                    "\n  longitudinal: " + ((float)filteredData.gforce_longitudinal).ToString("F2") +
                    "\n  pitch / roll: " + ((float)filteredData.pitch).ToString("F3") + " / " + ((float)filteredData.roll).ToString("F3") +
                    "\n\n";
                ui.DebugTextChanged(head + JsonConvert.SerializeObject(filteredData, Formatting.Indented) +
                    "\n dt: " + dt +
                    "\n fwd: " + fwd.X + ", " + fwd.Y + ", " + fwd.Z +
                    "\n up: " + up.X + ", " + up.Y + ", " + up.Z);
            }

            SendFilteredData();

            return true;
        }

        public override void CalcPosition()
        {
            currRawPos = new Vector3(transform.M41, transform.M42, transform.M43);

            rawData.position_x = currRawPos.X;
            rawData.position_y = currRawPos.Y;
            rawData.position_z = currRawPos.Z;

            FilterModuleCustom.Instance.Filter(rawData, ref filteredData, posKeyMask, true);

            worldPosition = new Vector3((float)filteredData.position_x, (float)filteredData.position_y, (float)filteredData.position_z);
        }

        public override void CalcVelocity()
        {
            Vector3 worldVelocity = (worldPosition - lastPosition) / dt;
            lastWorldVelocity = worldVelocity;

            lastPosition = transform.Translation = worldPosition;

            Matrix4x4 rotation = transform;
            rotation.M41 = 0.0f;
            rotation.M42 = 0.0f;
            rotation.M43 = 0.0f;
            rotation.M44 = 1.0f;

            rotInv = new Matrix4x4();
            Matrix4x4.Invert(rotation, out rotInv);

            Vector3 localVelocity = Vector3.Transform(worldVelocity, rotInv);

            rawData.local_velocity_x = localVelocity.X;
            rawData.local_velocity_y = localVelocity.Y;
            rawData.local_velocity_z = localVelocity.Z;
        }

        public override void FilterVelocity()
        {
            FilterModuleCustom.Instance.Filter(rawData, ref filteredData, velKeyMask, false);
        }

        public override void CalcAngles()
        {
            Quaternion quat = Quaternion.CreateFromRotationMatrix(transform);

            Vector3 pyr = Utils.GetPYRFromQuaternion(quat);

            rawData.pitch = -pyr.X;
            rawData.yaw = -pyr.Y;
            rawData.roll = Utils.LoopAngleRad(-pyr.Z, (float)Math.PI * 0.5f);
        }

        // DriveClub: the game's own accelerations rather than ones worked out
        // from the transform; an impact adds a jolt to the heave.
        public override void CalcAcceleration()
        {
            if (!frame.HasDynamics || !frame.InRace)
            {
                base.CalcAcceleration();
                return;
            }

            Vector3 localVelocity = new Vector3((float)filteredData.local_velocity_x, (float)filteredData.local_velocity_y, (float)filteredData.local_velocity_z);
            lastVelocity = localVelocity;

            const float toG = 0.10197162129779283f;
            rawData.gforce_lateral = frame.accelRight * toG;
            rawData.gforce_vertical = frame.accelUp * toG + Math.Max(0.0f, frame.impact);
            rawData.gforce_longitudinal = frame.accelForward * toG;

            FilterModuleCustom.Instance.Filter(rawData, ref filteredData, accelKeyMask, false);
        }

        // DriveClub: each wheel's travel from its tyre's load (bumps, kerbs and
        // the car's own lean), rather than one worked out from the g-forces.
        public override void SimulateSuspension()
        {
            if (!frame.HasSuspension || !frame.InRace)
            {
                base.SimulateSuspension();
                return;
            }

            rawData.suspension_position_fl = filteredData.suspension_position_fl = frame.suspension[0];
            rawData.suspension_position_fr = filteredData.suspension_position_fr = frame.suspension[1];
            rawData.suspension_position_bl = filteredData.suspension_position_bl = frame.suspension[2];
            rawData.suspension_position_br = filteredData.suspension_position_br = frame.suspension[3];

            rawData.suspension_velocity_bl = ((float)filteredData.suspension_position_bl - (float)lastFilteredData.suspension_position_bl) / dt;
            rawData.suspension_velocity_br = ((float)filteredData.suspension_position_br - (float)lastFilteredData.suspension_position_br) / dt;
            rawData.suspension_velocity_fl = ((float)filteredData.suspension_position_fl - (float)lastFilteredData.suspension_position_fl) / dt;
            rawData.suspension_velocity_fr = ((float)filteredData.suspension_position_fr - (float)lastFilteredData.suspension_position_fr) / dt;

            FilterModuleCustom.Instance.Filter(rawData, ref filteredData, suspVelKeyMask, false);

            rawData.suspension_acceleration_bl = ((float)filteredData.suspension_velocity_bl - (float)lastFilteredData.suspension_velocity_bl) / dt;
            rawData.suspension_acceleration_br = ((float)filteredData.suspension_velocity_br - (float)lastFilteredData.suspension_velocity_br) / dt;
            rawData.suspension_acceleration_fl = ((float)filteredData.suspension_velocity_fl - (float)lastFilteredData.suspension_velocity_fl) / dt;
            rawData.suspension_acceleration_fr = ((float)filteredData.suspension_velocity_fr - (float)lastFilteredData.suspension_velocity_fr) / dt;

            SetWheelSpeeds();
        }

        // Each wheel's speed from the car's and its tyre's slip ratio (when the
        // game sends it): wheelspin and locking brakes, for shakers.
        void SetWheelSpeeds()
        {
            float v = (float)filteredData.local_velocity_z;
            if (!frame.HasSlip || !frame.InRace)
            {
                rawData.wheel_patch_speed_fl = rawData.wheel_patch_speed_fr = v;
                rawData.wheel_patch_speed_bl = rawData.wheel_patch_speed_br = v;
                filteredData.wheel_patch_speed_fl = filteredData.wheel_patch_speed_fr = v;
                filteredData.wheel_patch_speed_bl = filteredData.wheel_patch_speed_br = v;
                return;
            }
            float[] w = new float[4];
            for (int i = 0; i < 4; ++i)
                w[i] = v * (1.0f + Math.Max(-1.0f, Math.Min(5.0f, frame.slipRatio[i])));
            rawData.wheel_patch_speed_fl = filteredData.wheel_patch_speed_fl = w[0];
            rawData.wheel_patch_speed_fr = filteredData.wheel_patch_speed_fr = w[1];
            rawData.wheel_patch_speed_bl = filteredData.wheel_patch_speed_bl = w[2];
            rawData.wheel_patch_speed_br = filteredData.wheel_patch_speed_br = w[3];
        }

        float maxRpmSeen = 0.0f;

        public override void SimulateEngine()
        {
            bool known = frame.InputsKnown && frame.rpm >= 0.0f;
            if (known && frame.rpm > maxRpmSeen && frame.rpm < 20000.0f)
                maxRpmSeen = frame.rpm;
            // The game does not say its car's limit: the highest RPM seen, at least 7000.
            rawData.max_rpm = Math.Max(7000.0f, maxRpmSeen);
            rawData.max_gears = 6.0f;
            rawData.gear = known && frame.gear >= 0.0f ? frame.gear : 1.0f;
            rawData.idle_rpm = 800.0f;

            Vector3 localVelocity = new Vector3((float)filteredData.local_velocity_x, (float)filteredData.local_velocity_y, (float)filteredData.local_velocity_z);
            filteredData.speed = rawData.speed = localVelocity.Length();
        }

        public override void ProcessInputs()
        {
            if (frame.InputsKnown)
            {
                filteredData.throttle_input = Math.Max(0.0f, frame.throttle);
                filteredData.brake_input = Math.Max(0.0f, frame.brake);
                filteredData.steering_input = frame.steering;
                if (frame.version >= 2 && frame.clutch >= 0.0f)
                    filteredData.clutch_input = frame.clutch;
                filteredData.engine_rate = frame.rpm >= 0.0f ? frame.rpm : (Math.Max(0.0f, frame.throttle) * 6200.0f) + 800.0f;
                return;
            }

            // Not sent by the game yet: the pad on this PC, as for the other games.
            base.ProcessInputs();
        }

        public override void CalcAngularVelocityAndAccel()
        {
            Matrix4x4 lastTransformLocal = Matrix4x4.Multiply(lastTransform, rotInv);

            Vector3 lastRht = new Vector3(lastTransformLocal.M11, lastTransformLocal.M12, lastTransformLocal.M13);
            Vector3 lastUp = new Vector3(lastTransformLocal.M21, lastTransformLocal.M22, lastTransformLocal.M23);
            Vector3 lastFwd = new Vector3(lastTransformLocal.M31, lastTransformLocal.M32, lastTransformLocal.M33);

            Vector3 fwdProjX = Vector3.Normalize(new Vector3(0.0f, lastFwd.Y, lastFwd.Z));
            Vector3 fwdProjY = Vector3.Normalize(new Vector3(lastFwd.X, 0.0f, lastFwd.Z));
            Vector3 rhtProjZ = Vector3.Normalize(new Vector3(lastRht.X, lastRht.Y, 0.0f));

            Vector3 localRht = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 localFwd = new Vector3(0.0f, 0.0f, 1.0f);

            float yawVel = (float)Math.Acos(Clamp(Vector3.Dot(fwdProjY, localFwd))) * Math.Sign(Vector3.Dot(lastFwd, localRht)) / dt;
            float pitchVel = (float)Math.Acos(Clamp(Vector3.Dot(fwdProjX, localFwd))) * Math.Sign(Vector3.Dot(lastUp, localFwd)) / dt;
            float rollVel = (float)Math.Acos(Clamp(Vector3.Dot(rhtProjZ, localRht))) * Math.Sign(Vector3.Dot(lastUp, localRht)) / dt;

            rawData.yaw_velocity = yawVel;
            rawData.pitch_velocity = pitchVel;
            rawData.roll_velocity = rollVel;

            FilterModuleCustom.Instance.Filter(rawData, ref filteredData, angVelKeyMask, false);

            rawData.yaw_acceleration = ((float)filteredData.yaw_velocity - (float)lastFilteredData.yaw_velocity) / dt;
            rawData.pitch_acceleration = ((float)filteredData.pitch_velocity - (float)lastFilteredData.pitch_velocity) / dt;
            rawData.roll_acceleration = ((float)filteredData.roll_velocity - (float)lastFilteredData.roll_velocity) / dt;
        }

        // A dot product a hair past 1 (rounding) makes Acos NaN.
        static double Clamp(float value)
        {
            return Math.Max(-1.0, Math.Min(1.0, (double)value));
        }
    }
}
