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
    /// <para><b>Momentum is the truth (build brief S2/S6).</b> Thrust changes velocity; with no thrust the
    /// ship keeps drifting. To slow down you must turn and burn the other way ("flip-and-burn").</para>
    /// <para><b>Flight-assist (S6).</b> With assist ON, the flight computer fires counter-thrusters to bleed
    /// off sideways drift and counter-torque to stop unwanted spin, and brakes when you release the
    /// throttle — responsive and forgiving. With assist OFF you get the raw Newtonian model: drift through
    /// turns, retain all momentum, and decelerate only by thrusting. Toggle with <c>T</c>.</para>
    /// </remarks>
    public sealed class ShipController
    {
        // --- Tuning (would live in Spectre/Data JSON later; constants for now). Values target the Spectre:
        // light (11,000 kg), strongly over-thrust for its class, very nimble (S10.4/S18). ---
        private const double ForwardThrust = 520_000.0;   // N  (~47 m/s^2)
        private const double StrafeThrust = 280_000.0;    // N
        private const double BoostMultiplier = 2.5;
        private const double MouseTorquePerPixel = 1_400.0; // N·m per pixel of mouse movement
        private const double RollTorque = 90_000.0;        // N·m
        private const double LateralDamping = 2.2;          // 1/s — assist: bleed sideways drift
        private const double BrakeDamping = 1.2;            // 1/s — assist: brake when no throttle
        private const double AngularDamping = 4.0;          // 1/s — assist: stop unwanted spin

        /// <summary>The ship's physical body.</summary>
        public RigidBody Ship { get; }

        /// <summary>Whether the forgiving flight-assist layer is active (default on).</summary>
        public bool FlightAssist { get; set; } = true;

        private Vector3d _thrustLocal;   // intent for the current frame, in the ship's frame
        private Vector3d _torqueLocal;
        private System.Numerics.Vector2 _lastMouse;
        private bool _hasLastMouse;
        private bool _previousToggle;

        public ShipController(RigidBody ship)
        {
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            Ship.Mass = 11_000.0;
            Ship.InertiaScalar = 9_000.0;
        }

        /// <summary>Samples input once per rendered frame into a thrust/torque intent for the fixed steps.</summary>
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

            // Mouse steers pitch (X) and yaw (Y); Q/E roll (Z).
            System.Numerics.Vector2 position = mouse.Position;
            double dx = 0, dy = 0;
            if (_hasLastMouse)
            {
                dx = position.X - _lastMouse.X;
                dy = position.Y - _lastMouse.Y;
            }
            _lastMouse = position;
            _hasLastMouse = true;

            var torque = new Vector3d(-dy * MouseTorquePerPixel, dx * MouseTorquePerPixel, 0);
            if (keyboard.IsKeyPressed(Key.Q)) torque += new Vector3d(0, 0, RollTorque);
            if (keyboard.IsKeyPressed(Key.E)) torque += new Vector3d(0, 0, -RollTorque);
            _torqueLocal = torque;

            // Toggle flight-assist on the T key's rising edge.
            bool toggle = keyboard.IsKeyPressed(Key.T);
            if (toggle && !_previousToggle) FlightAssist = !FlightAssist;
            _previousToggle = toggle;
        }

        /// <summary>Applies the current intent (plus flight-assist) and integrates one fixed step.</summary>
        public void FixedStep(double dt)
        {
            if (!_thrustLocal.IsZero()) Ship.ApplyForceLocal(_thrustLocal);
            if (!_torqueLocal.IsZero()) Ship.ApplyTorqueLocal(_torqueLocal);

            if (FlightAssist)
            {
                ApplyAngularAssist();
                ApplyLinearAssist();
            }

            Ship.Integrate(dt);
        }

        // Stop unwanted spin: when the pilot is not commanding rotation, fire counter-torque.
        private void ApplyAngularAssist()
        {
            if (_torqueLocal.IsZero() && !Ship.AngularVelocity.IsZero())
            {
                Ship.ApplyTorque(Ship.AngularVelocity * (-AngularDamping * Ship.InertiaScalar));
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
