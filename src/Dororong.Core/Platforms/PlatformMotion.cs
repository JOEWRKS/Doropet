using Dororong.Core.Geometry;

namespace Dororong.Core.Platforms;

public enum PlatformPhase { Suspended, Supported, Falling, Landing, Lifting }
public readonly record struct PlatformPose(PlatformPhase Phase, PointD Position,
    SurfaceKey? Support, double Squash, double Sway)
{
    public bool IsExtremeLanding { get; init; }
    public double LegSpread { get; init; }
}
public readonly record struct PlatformMotionInput(TimeSpan Delta, PointD DisplayedPosition,
    PointD DesiredPosition, FootContact LocalContact,
    IReadOnlyList<PlatformSurface> Surfaces, bool DirectOwnsPosition, bool SceneReliable)
{
    public IReadOnlyList<DesktopMonitor>? Monitors { get; init; }
    public double SupportedOffsetY { get; init; }
}

public sealed class PlatformMotion
{
    private PlatformSurface? support;
    private PlatformSurface[] previousBars = Array.Empty<PlatformSurface>();
    private double velocity;
    private double phaseSeconds;
    private double liftStartY;
    private double sway;
    private double fallDistance;
    private double landingStrength;
    private bool extremeLanding;
    private const double LandingSeconds = .45;
    private const double ExtremeCompression = .82 * .70;

    public PlatformPose Current { get; private set; }

    // Coordinate-frame change only: world surfaces, velocity, impact energy and
    // phase clock survive. The caller shifts local contact by the opposite amount.
    public void RebasePosition(PointD offset)
    {
        if (!Valid(offset)) throw new ArgumentOutOfRangeException(nameof(offset));
        Current = Current with { Position = Current.Position + offset };
        liftStartY += offset.Y;
    }

    /// <summary>The runtime calls this after its separate monitor-topology clamp.</summary>
    public void Reset(PointD displayedPosition)
    {
        support = null;
        velocity = phaseSeconds = sway = 0;
        fallDistance = landingStrength = 0;
        extremeLanding = false;
        previousBars = Array.Empty<PlatformSurface>();
        Current = new(PlatformPhase.Suspended, displayedPosition, null, 0, 0);
    }

    public PlatformPose Advance(PlatformMotionInput input)
    {
        if (input.DirectOwnsPosition)
        {
            Reset(input.DesiredPosition);
            if (input.SceneReliable) RememberBars(input.Surfaces);
            return Current;
        }

        // Runtime bounds transient uncertainty and supplies reliable floors on expiry.
        // Retain the old owner origin so recovery applies its translation exactly once.
        if (!input.SceneReliable || !Valid(input.DisplayedPosition) || !Valid(input.DesiredPosition) ||
            !Valid(input.LocalContact))
            return Current = Current with { Position = Valid(input.DisplayedPosition) ? input.DisplayedPosition : Current.Position };

        var seconds = Math.Clamp(input.Delta.TotalSeconds, 0, .25);
        sway *= Math.Exp(-10 * seconds);
        var position = input.DisplayedPosition;
        var walkingX = input.DesiredPosition.X - position.X;
        var surfaces = input.Surfaces.Where(Valid).ToArray();
        var phase = Current.Phase;
        SurfaceKey? releasedKey = null;

        if (support is { } previous)
        {
            PlatformSurface? retained = null;
            PointD supportedPosition = position;
            foreach (var candidate in surfaces)
            {
                if (candidate.Key != previous.Key || candidate.MonitorId != previous.MonitorId) continue;
                var translated = position + (candidate.OwnerOrigin - previous.OwnerOrigin);
                translated = new(translated.X + walkingX, candidate.Top - input.LocalContact.SoleY);
                if (!Overlaps(candidate, translated, input.LocalContact)) continue;
                retained = candidate;
                supportedPosition = translated;
                break;
            }

            if (retained is { } currentSupport)
            {
                sway = Math.Clamp(sway + (currentSupport.OwnerOrigin.X - previous.OwnerOrigin.X) * .001, -.03, .03);
                support = currentSupport;
                if (phase == PlatformPhase.Lifting)
                {
                    phaseSeconds = Math.Min(.16, phaseSeconds + seconds);
                    var p = phaseSeconds / .16;
                    var blend = p * p * (3 - 2 * p);
                    position = new(supportedPosition.X,
                        liftStartY + (currentSupport.Top - input.LocalContact.SoleY - liftStartY) * blend);
                    if (p >= 1) phase = PlatformPhase.Supported;
                    RememberBars(surfaces);
                    return Set(phase, position);
                }
                position = supportedPosition;
            }
            else
            {
                releasedKey = previous.Key;
                support = null;
                velocity = phaseSeconds = 0;
                fallDistance = landingStrength = 0;
                sway = 0;
                phase = PlatformPhase.Falling;
            }
        }

        if (support is null && releasedKey is null)
        {
            // Acquisition is by actual contact, never by a remembered HWND alone.
            foreach (var surface in surfaces)
            {
                if (Math.Abs(surface.Top - (position.Y + input.LocalContact.SoleY)) > .001 ||
                    !Overlaps(surface, position, input.LocalContact)) continue;
                support = surface;
                velocity = phaseSeconds = 0;
                fallDistance = landingStrength = 0;
                phase = PlatformPhase.Supported;
                position = new(position.X, surface.Top - input.LocalContact.SoleY);
                break;
            }
        }

        var monitor = support?.MonitorId ?? surfaces
            .Where(s => s.Kind == PlatformKind.Floor && Overlaps(s, position, input.LocalContact))
            .OrderBy(s => Math.Abs(s.Top - (position.Y + input.LocalContact.SoleY)))
            .Select(s => (long?)s.MonitorId).FirstOrDefault();
        foreach (var bar in surfaces)
        {
            if (bar.Kind != PlatformKind.Taskbar || bar.MonitorId != monitor ||
                !IntersectsBar(bar, input.DisplayedPosition, input.LocalContact) ||
                bar.Top >= input.DisplayedPosition.Y + input.LocalContact.SoleY ||
                (support is { } lower && lower.Top < bar.Top) ||
                previousBars.Any(old => old.Key == bar.Key && old.MonitorId == bar.MonitorId &&
                    IntersectsBar(old, input.DisplayedPosition, input.LocalContact))) continue;

            support = bar;
            velocity = 0;
            fallDistance = landingStrength = 0;
            liftStartY = input.DisplayedPosition.Y;
            phaseSeconds = seconds;
            var p = Math.Min(1, phaseSeconds / .16);
            var blend = p * p * (3 - 2 * p);
            position = new(position.X, liftStartY + (bar.Top - input.LocalContact.SoleY - liftStartY) * blend);
            RememberBars(surfaces);
            return Set(p >= 1 ? PlatformPhase.Supported : PlatformPhase.Lifting, position);
        }
        RememberBars(surfaces);

        if (support is not null)
        {
            if (phase == PlatformPhase.Landing)
            {
                var duration = extremeLanding ? 1.8 : LandingSeconds;
                phaseSeconds = Math.Min(duration, phaseSeconds + seconds);
                if (phaseSeconds >= duration) phase = PlatformPhase.Supported;
            }
            // A click hop is an offset from the retained support baseline, not
            // free fall. Reconstructing that baseline each tick prevents drift.
            if (phase == PlatformPhase.Supported && double.IsFinite(input.SupportedOffsetY))
                position = position with { Y = position.Y + Math.Clamp(input.SupportedOffsetY, -12, 0) };
            return Set(phase, position);
        }

        phase = PlatformPhase.Falling;
        var remaining = seconds;
        // After support loss the first fall starts at the last displayed location.
        var targetX = releasedKey is null ? position.X + walkingX : position.X;
        var collisionSurfaces = releasedKey is null ? surfaces : surfaces.Where(s => s.Key != releasedKey).ToArray();
        while (remaining > 0)
        {
            var dt = Math.Min(.008, remaining);
            var nextVelocity = Math.Min(1200, velocity + 1800 * dt);
            var next = new PointD(position.X + (targetX - position.X) * (dt / remaining),
                position.Y + (velocity + nextVelocity) * .5 * dt);
            var hit = PlatformGeometry.FirstCrossing(collisionSurfaces,
                WorldContact(position, input.LocalContact), WorldContact(next, input.LocalContact));
            if (hit is { } landed)
            {
                var t = (landed.Top - (position.Y + input.LocalContact.SoleY)) / (next.Y - position.Y);
                var landedY = landed.Top - input.LocalContact.SoleY;
                fallDistance += Math.Max(0, landedY - position.Y);
                landingStrength = Math.Clamp(fallDistance / 300, .12, 1);
                var screenHeight = input.Monitors?.FirstOrDefault(m => m.Id == landed.MonitorId)?.Bounds.Height ?? 0;
                extremeLanding = double.IsFinite(screenHeight) && screenHeight > 0 && fallDistance + 1e-7 >= screenHeight * .8;
                position = new(position.X + (next.X - position.X) * t, landedY);
                support = landed;
                velocity = phaseSeconds = 0;
                return Set(PlatformPhase.Landing, position);
            }
            fallDistance += Math.Max(0, next.Y - position.Y);
            position = next;
            velocity = nextVelocity;
            remaining -= dt;
        }
        return Set(phase, position);
    }

    private PlatformPose Set(PlatformPhase phase, PointD position)
    {
        var squash = 0d;
        var spread = 0d;
        if (phase == PlatformPhase.Landing && extremeLanding)
        {
            var t = phaseSeconds;
            // Reach full compression, hold it for one whole second, then spring
            // back. This is one supported landing, never a second free fall.
            squash = t switch
            {
                < .10 => ExtremeCompression * ImpactExpansion(t),
                < 1.10 => ExtremeCompression,
                < 1.28 => ExtremeCompression - (ExtremeCompression + .16) * Smooth((t - 1.10) / .18),
                < 1.60 => -.16 * (1 - Smooth((t - 1.28) / .32)),
                < 1.68 => .12 * Smooth((t - 1.60) / .08),
                _ => .12 * (1 - Smooth((t - 1.68) / .12))
            };
            spread = ImpactExpansion(t) * (1 - Smooth((t - 1.10) / .28));
            if (t > 1.10 && t < 1.60)
            {
                var arc = Math.Sin(Math.PI * (t - 1.10) / .50);
                position = position with { Y = position.Y - 32 * arc * arc };
            }
            return Current = new(phase, position, support?.Key, squash, sway)
                { IsExtremeLanding = true, LegSpread = spread };
        }
        if (phase == PlatformPhase.Landing)
        {
            var t = phaseSeconds;
            squash = landingStrength * (t switch
            {
                < .08 => .30 * Smooth(t / .08),
                < .14 => .30 - .40 * Smooth((t - .08) / .06),
                < .32 => -.10 * (1 - Smooth((t - .14) / .18)),
                < .38 => .08 * Smooth((t - .32) / .06),
                _ => .08 * (1 - Smooth((t - .38) / .07))
            });
            // Keep the surface owner throughout this one landing sequence, but
            // move the host upward for a real visible hop. Next tick's retained
            // support reconstructs its baseline, never accumulates this offset.
            if (t > .08 && t < .32)
            {
                var arc = Math.Sin(Math.PI * (t - .08) / .24);
                position = position with { Y = position.Y - 18 * landingStrength * arc * arc };
            }
        }
        return Current = new(phase, position, support?.Key, squash, sway);
    }

    private static double ImpactExpansion(double seconds)
    {
        // Fast ease-out into the approved maximum, a small elastic recoil,
        // then settle. Never deepen the user's reduced compression peak.
        if (seconds < .035) return 1 - Math.Pow(1 - Math.Clamp(seconds / .035, 0, 1), 3);
        if (seconds < .065) return 1 - .06 * Smooth((seconds - .035) / .030);
        if (seconds < .100) return .94 + .06 * Smooth((seconds - .065) / .035);
        return 1;
    }

    private static double Smooth(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return p * p * (3 - 2 * p);
    }

    private void RememberBars(IReadOnlyList<PlatformSurface> surfaces) =>
        previousBars = surfaces.Where(s => s.Kind == PlatformKind.Taskbar && Valid(s)).ToArray();

    private static bool IntersectsBar(PlatformSurface bar, PointD position, FootContact contact) =>
        bar.Bottom is { } bottom && double.IsFinite(bottom) && bottom >= bar.Top &&
        position.Y + contact.SoleY >= bar.Top && position.Y + contact.SoleY <= bottom &&
        Overlaps(bar, position, contact);

    private static bool Overlaps(PlatformSurface surface, PointD position, FootContact contact) =>
        Math.Min(surface.Right, position.X + contact.Right) - Math.Max(surface.Left, position.X + contact.Left) >= 2;

    private static FootContact WorldContact(PointD position, FootContact local) =>
        new(position.X + local.Left, position.X + local.Right,
            position.Y + local.SoleY, position.Y + local.VisibleTop);

    private static bool Valid(PointD point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool Valid(FootContact contact) => double.IsFinite(contact.Left) && double.IsFinite(contact.Right) &&
        double.IsFinite(contact.SoleY) && double.IsFinite(contact.VisibleTop) && contact.Right >= contact.Left;
    private static bool Valid(PlatformSurface surface) => double.IsFinite(surface.Left) && double.IsFinite(surface.Right) &&
        double.IsFinite(surface.Top) && Valid(surface.OwnerOrigin) && surface.Right > surface.Left;
}
