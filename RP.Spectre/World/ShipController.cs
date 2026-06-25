namespace RP.Spectre.World
{
    using System;
    using RP.Game.Physics;
    using RP.Math;
    using Silk.NET.Input;

    /// <summary>
    /// Flies the player ship under Newtonian 6-DoF physics: it reads input and applies thrust and torque to
    /// a <see cref="RigidBody"/>, with an optional <b>flight-assist</b> layer that makes the raw simulation
    /// forgiving. This is Spectre-specific (the control mapping and tuning of <i>this</i> ship); the
    /// physics it drives is the generic engine <see cref="RigidBody"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Mouse as a rate stick (the feel fix).</b> The mouse no longer dumps raw per-pixel torque into
    /// the body (which spun up without bound and read as "too fast / reversed"). Instead each frame's mouse
    /// motion sets a <i>desired turn rate</i> — clamped to a sane maximum — and the flight computer drives the
    /// ship's angular velocity toward it with a proportional controller. The result is crisp, bounded, and
    /// frame-rate independent: nudge to turn slowly, flick to turn hard, release to stop. Pitch is
    /// non-inverted by default (mouse up = nose up) with an invert option; sensitivity is a user setting.</para>
    /// <para><b>Momentum is the truth (build brief S2/S6).</b> Thrust changes velocity; with no thrust the
    /// ship keeps drifting. To slow down you must turn and burn the other way ("flip-and-burn").</para>
    /// <para><b>Flight-assist (S6).</b> With assist ON, the flight computer bleeds off sideways drift, brakes
    /// when you release the throttle, and rate-controls rotation (above) — responsive and forgiving. With
    /// assist OFF you get the raw Newtonian model: drift through turns, retain all momentum, and rotation is
    /// direct torque that keeps spinning until you counter it. Toggle with <c>T</c>.</para>
    /// </remarks>
    public sealed class ShipController
    {
        // --- Tuning. Values target the Spectre: light (11,000 kg), strongly over-thrust for its class, very
        // nimble (S10.4/S18). ---
        private const double ForwardThrust = 520_000.0;   // N  (~47 m/s^2)
        private const double StrafeThrust = 280_000.0;    // N
        private const double BoostMultiplier = 2.5;

        // Mouse rate steering: a pixel of motion maps to this many rad/s of desired turn rate (scaled by the
        // user's sensitivity multiplier), clamped to MaxTurnRate. Tuned so a small nudge is a gentle turn and
        // a fast flick saturates at a hard-but-controllable rate.
        private const double MouseRatePerPixel = 0.045;
        private const double MaxTurnRate = 2.2;            // rad/s — hard cap on commanded pitch/yaw
        private const double RollRate = 1.8;               // rad/s — commanded by Q/E
        private const double RateControlGain = 9.0;        // 1/s — how hard assist drives toward the target rate
        private const double RawTorquePerRate = 1.0;       // assist-off: torque = rate * this * inertia

        private const double LateralDamping = 2.2;          // 1/s — assist: bleed sideways drift
        private const double BrakeDamping = 1.2;            // 1/s — assist: brake when no throttle

        /// <summary>The ship's physical body.</summary>
        public RigidBody Ship { get; }

        /// <summary>Whether the forgiving flight-assist layer is active (default on).</summary>
        public bool FlightAssist { get; set; } = true;

        /// <summary>User sensitivity multiplier on mouse turn rate (1 = the tuned default).</summary>
        public double MouseSensitivity { get; set; } = 1.0;

        /// <summary>When true, moving the mouse up pitches the nose down (classic-flightstick feel).</summary>
        public bool InvertPitch { get; set; }

        /// <summary>Whether the mouse is captured for steering. Toggled with <c>Esc</c> so the player can free
        /// the cursor; the host applies the actual OS cursor lock from this flag.</summary>
        public bool MouseCaptured { get; set; } = true;

        private Vector3d _thrustLocal;     // intent for the current frame, in the ship's frame
        private Vector3d _targetBodyRate;  // desired pitch/yaw/roll rate (rad/s) in the ship's frame
        private System.Numerics.Vector2 _lastMouse;
        private bool _hasLastMouse;
        private bool _previousAssistToggle;
        private bool _previousCaptureToggle;
        private bool _previousInvertToggle;

        public ShipController(RigidBody ship)
        {
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            Ship.Mass = 11_000.0;
            Ship.InertiaScalar = 9_000.0;
        }

        /// <summary>Forgets the last mouse position, so the next frame's steering delta is zero. Call after a
        /// cursor warp (capture toggle, window resize) to avoid a one-frame fling.</summary>
        public void ResetMouseBaseline() => _hasLastMouse = false;

        /// <summary>Samples input once per rendered frame into a thrust/rate intent for the fixed steps.</summary>
        public void ReadControls(IKeyboard keyboard, IMouse mouse)
        {
            bool boost = keyboard.IsKeyPressed(Key.ShiftLeft);
            double fwd = (boost ? BoostMultiplier : 1.0) * ForwardThrust;

            // Local frame: forward is -Z, right +X, up +Y.
            var thrust = Vector3d.Origin;
            if (keyboard.IsKeyPressed(Key.W)) thrust += new Vector3d(0, 0, -fwd);
            if (keyboard.IsKeyPressed(Key.S)) thrust += new Vector3d(0, 0, ForwardThrust);
            if (keyboard.IsKeyPressed(Key.D)) thrust += new Vector3d(StrafeThrust, 0, 0);
            if (keyboard.IsKeyPressed(Key.A)) thrust += new Vector3d(-StrafeThrust, 0, 0);
            if (keyboard.IsKeyPressed(Key.Space)) thrust += new Vector3d(0, StrafeThrust, 0);
            if (keyboard.IsKeyPressed(Key.ControlLeft)) thrust += new Vector3d(0, -StrafeThrust, 0);
            _thrustLocal = thrust;

            // Esc toggles cursor capture so the player can reclaim the mouse without alt-tabbing.
            bool captureToggle = keyboard.IsKeyPressed(Key.Escape);
            if (captureToggle && !_previousCaptureToggle)
            {
                MouseCaptured = !MouseCaptured;
                _hasLastMouse = false; // avoid a jump on the frame capture resumes
            }
            _previousCaptureToggle = captureToggle;

            // Invert-pitch toggle on the I key's rising edge.
            bool invertToggle = keyboard.IsKeyPressed(Key.I);
            if (invertToggle && !_previousInvertToggle) InvertPitch = !InvertPitch;
            _previousInvertToggle = invertToggle;

            // Mouse motion -> desired turn rate (only while captured).
            double dx = 0, dy = 0;
            System.Numerics.Vector2 position = mouse.Position;
            if (MouseCaptured)
            {
                if (_hasLastMouse)
                {
                    dx = position.X - _lastMouse.X;
                    dy = position.Y - _lastMouse.Y;
                }
                _hasLastMouse = true;
            }
            _lastMouse = position;

            double sens = MouseRatePerPixel * Math.Max(0.05, MouseSensitivity);
            // Pitch about +X: mouse up (dy < 0) -> nose up by default. Yaw about Y: mouse right -> nose right
            // (a rotation about -Y), so the Y component is -dx. Both clamped to a controllable maximum.
            double pitch = Clamp((InvertPitch ? dy : -dy) * sens, MaxTurnRate);
            double yaw = Clamp(-dx * sens, MaxTurnRate);

            double roll = 0;
            if (keyboard.IsKeyPressed(Key.Q)) roll += RollRate;
            if (keyboard.IsKeyPressed(Key.E)) roll -= RollRate;

            _targetBodyRate = new Vector3d(pitch, yaw, roll);

            // Toggle flight-assist on the T key's rising edge.
            bool assistToggle = keyboard.IsKeyPressed(Key.T);
            if (assistToggle && !_previousAssistToggle) FlightAssist = !FlightAssist;
            _previousAssistToggle = assistToggle;
        }

        private static double Clamp(double v, double limit) => v < -limit ? -limit : (v > limit ? limit : v);

        /// <summary>Applies the current intent (plus flight-assist) and integrates one fixed step.</summary>
        public void FixedStep(double dt)
        {
            if (!_thrustLocal.IsZero()) Ship.ApplyForceLocal(_thrustLocal);

            ApplyRotation();

            if (FlightAssist) ApplyLinearAssist();

            Ship.Integrate(dt);
        }

        // Rotation control. With assist: a proportional controller drives the body's angular velocity toward
        // the commanded rate, so releasing the mouse stops the turn crisply. Without assist: the commanded
        // rate becomes raw torque, so spin builds and persists (Newtonian truth).
        private void ApplyRotation()
        {
            if (FlightAssist)
            {
                // Express the world-space angular velocity in the body frame, then chase the target rate.
                Vector3d bodyRate = Ship.Orientation.Conjugate().Rotate(Ship.AngularVelocity);
                Vector3d error = _targetBodyRate - bodyRate;
                Ship.ApplyTorqueLocal(error * (RateControlGain * Ship.InertiaScalar));
            }
            else if (!_targetBodyRate.IsZero())
            {
                Ship.ApplyTorqueLocal(_targetBodyRate * (RawTorquePerRate * Ship.InertiaScalar));
            }
        }

        // Bleed sideways drift always; brake forward/back motion too when the throttle is released.
        private void ApplyLinearAssist()
        {
            Vector3d forward = Ship.Forward;
            Vector3d velocity = Ship.Velocity;
            Vector3d along = forward * velocity.DotProduct(forward);
            Vector3d lateral = velocity - along;

            Vector3d counter = lateral * (-LateralDamping * Ship.Mass);
            if (_thrustLocal.IsZero())
            {
                counter += along * (-BrakeDamping * Ship.Mass);
            }

            Ship.ApplyForce(counter);
        }
    }
}
