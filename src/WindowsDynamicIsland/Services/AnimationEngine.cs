using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsDynamicIsland.Services
{
    /// <summary>
    /// Frame-rate-independent critically damped spring animation engine using 2ms fixed-substep integration.
    /// Produces Apple CoreAnimation-grade smooth trajectories across 60Hz/120Hz/144Hz/240Hz displays
    /// and detaches completely when settled (0% idle CPU/GPU).
    /// </summary>
    public sealed class AnimationEngine
    {
        private struct SpringChannel
        {
            public double Current;
            public double Target;
            public double Velocity;

            public void Snap(double value)
            {
                Current = value;
                Target = value;
                Velocity = 0;
            }

            public bool StepSubdivided(double totalDt, double stiffness, double damping)
            {
                double diff = Current - Target;
                if (Math.Abs(diff) < 0.05 && Math.Abs(Velocity) < 0.35)
                {
                    Current = Target;
                    Velocity = 0;
                    return false;
                }

                const double maxSubStep = 0.002; // 500Hz physics integration for mathematical stability
                double remaining = totalDt;
                while (remaining > 0.00001)
                {
                    double step = remaining > maxSubStep ? maxSubStep : remaining;
                    double displacement = Current - Target;
                    double acceleration = (-stiffness * displacement) - (damping * Velocity);
                    Velocity += acceleration * step;
                    Current += Velocity * step;
                    remaining -= step;
                }

                if (Math.Abs(Current - Target) < 0.05 && Math.Abs(Velocity) < 0.35)
                {
                    Current = Target;
                    Velocity = 0;
                    return false;
                }

                return true;
            }
        }

        private SpringChannel _width;
        private SpringChannel _height;
        private SpringChannel _radius;
        private SpringChannel _opacity;

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly DispatcherTimer _watchdogTimer;
        private double _lastTimeSeconds;
        private bool _isSubscribed;
        private double _speedMultiplier = 1.0;

        public event Action<double, double, double, double> FrameUpdated;
        public event Action AnimationSettled;

        public double CurrentWidth => _width.Current;
        public double CurrentHeight => _height.Current;
        public double CurrentRadius => _radius.Current;
        public double CurrentOpacity => _opacity.Current;

        public double TargetWidth => _width.Target;
        public double TargetHeight => _height.Target;
        public double TargetRadius => _radius.Target;
        public bool IsAnimating => _isSubscribed;

        public AnimationEngine(double initialWidth, double initialHeight, double initialRadius, double initialOpacity = 1.0)
        {
            _width.Snap(initialWidth);
            _height.Snap(initialHeight);
            _radius.Snap(initialRadius);
            _opacity.Snap(initialOpacity);

            // Watchdog timer only steps if CompositionTarget.Rendering is throttled for >45ms (e.g. DWM occluded)
            _watchdogTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(32)
            };
            _watchdogTimer.Tick += (s, e) =>
            {
                if (_isSubscribed && (_stopwatch.Elapsed.TotalSeconds - _lastTimeSeconds) > 0.045)
                {
                    AdvancePhysicsFrame();
                }
            };
        }

        public void SetSpeedMultiplier(double multiplier)
        {
            _speedMultiplier = Math.Max(0.5, Math.Min(2.2, multiplier));
        }

        public void SnapTo(double width, double height, double radius, double opacity = 1.0)
        {
            _width.Snap(width);
            _height.Snap(height);
            _radius.Snap(radius);
            _opacity.Snap(opacity);
            StopLoop();
            FrameUpdated?.Invoke(_width.Current, _height.Current, _radius.Current, _opacity.Current);
        }

        public void AnimateTo(double targetWidth, double targetHeight, double targetRadius, double targetOpacity = 1.0)
        {
            _width.Target = targetWidth;
            _height.Target = targetHeight;
            _radius.Target = targetRadius;
            _opacity.Target = targetOpacity;

            if (!_isSubscribed)
            {
                _stopwatch.Restart();
                _lastTimeSeconds = 0;
                _isSubscribed = true;
                CompositionTarget.Rendering += OnCompositionRendering;
                _watchdogTimer.Start();
            }
        }

        private void OnCompositionRendering(object sender, EventArgs e)
        {
            AdvancePhysicsFrame();
        }

        private void AdvancePhysicsFrame()
        {
            double now = _stopwatch.Elapsed.TotalSeconds;
            double dt = now - _lastTimeSeconds;
            if (dt < 0.001) return;
            _lastTimeSeconds = now;

            if (dt > 0.05)
            {
                dt = 1.0 / 60.0;
            }

            dt *= _speedMultiplier;

            // Critically damped fluid Apple Dynamic Island spring: c = 2 * sqrt(k).
            // k = 225, c = 30.0 gives a silky-smooth, organic acceleration and deceleration curve without abrupt jumps or bounce.
            const double stiffness = 225.0;
            const double damping = 30.0;

            bool movingW = _width.StepSubdivided(dt, stiffness, damping);
            bool movingH = _height.StepSubdivided(dt, stiffness, damping);
            bool movingR = _radius.StepSubdivided(dt, 250.0, 31.6);
            bool movingO = _opacity.StepSubdivided(dt, 280.0, 33.5);

            FrameUpdated?.Invoke(_width.Current, _height.Current, _radius.Current, _opacity.Current);

            if (!movingW && !movingH && !movingR && !movingO)
            {
                StopLoop();
                AnimationSettled?.Invoke();
            }
        }

        private void StopLoop()
        {
            if (_isSubscribed)
            {
                _isSubscribed = false;
                CompositionTarget.Rendering -= OnCompositionRendering;
                _watchdogTimer.Stop();
                _stopwatch.Stop();
            }
        }
    }
}
