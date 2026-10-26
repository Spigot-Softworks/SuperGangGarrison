namespace OpenGarrison.Core;

// Freeze zones (a gameplay plugin's time stop): a projectile inside one skips its tick, so it
// holds still, its lifetime and fuse paused, and carries on as it was once the zone ends. A blade
// (a thrown knife) flies on until it is the zone's TravelPixels from where it was thrown, so
// knives thrown inside the zone leave the hand before they stop.
public sealed partial class ProjectileSystem
{
    private readonly record struct ProjectileFreezeZone(float X, float Y, float RadiusSquared, long StartFrame, long EndFrame, float TravelPixels);

    private readonly List<ProjectileFreezeZone> _freezeZones = new();

    // Whether any freeze zone is running this frame.
    public bool HasActiveFreezeZone
    {
        get
        {
            foreach (var zone in _freezeZones)
            {
                if (Frame >= zone.StartFrame && Frame < zone.EndFrame)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void AddProjectileFreezeZone(float x, float y, float radius, int ticks, float travelPixels = 0f)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(radius) || radius <= 0f || ticks <= 0)
        {
            return;
        }

        _freezeZones.Add(new ProjectileFreezeZone(x, y, radius * radius, Frame, Frame + ticks, float.IsFinite(travelPixels) ? Math.Max(0f, travelPixels) : 0f));
    }

    // Whether a point is inside a running freeze zone: sentries and dispensers there hold still too.
    public bool IsInFreezeZone(float x, float y) => IsProjectileFrozen(x, y);

    // traveled: how far the projectile has flown from where it was thrown (only blades know;
    // others count as far).
    private bool IsProjectileFrozen(float x, float y, float traveled = float.MaxValue)
    {
        for (var index = _freezeZones.Count - 1; index >= 0; index -= 1)
        {
            var zone = _freezeZones[index];
            // A zone from before the frame counter restarted (a new round) is over too.
            if (Frame >= zone.EndFrame || Frame < zone.StartFrame)
            {
                _freezeZones.RemoveAt(index);
                continue;
            }

            var deltaX = x - zone.X;
            var deltaY = y - zone.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= zone.RadiusSquared && traveled >= zone.TravelPixels)
            {
                return true;
            }
        }

        return false;
    }

    // A blade thrown inside a zone that has just flown past the zone's TravelPixels this tick is put
    // back at that distance, so it stops where the zone says rather than a whole tick further.
    private void ClampBladeToFreezeTravel(BladeProjectileEntity blade)
    {
        foreach (var zone in _freezeZones)
        {
            if (zone.TravelPixels <= 0f || Frame >= zone.EndFrame || Frame < zone.StartFrame)
            {
                continue;
            }

            var deltaX = blade.SpawnX - zone.X;
            var deltaY = blade.SpawnY - zone.Y;
            var traveled = blade.DistanceFromSpawn(blade.X, blade.Y);
            if ((deltaX * deltaX) + (deltaY * deltaY) > zone.RadiusSquared
                || traveled <= zone.TravelPixels
                || blade.DistanceFromSpawn(blade.PreviousX, blade.PreviousY) >= zone.TravelPixels)
            {
                continue;
            }

            var scale = zone.TravelPixels / traveled;
            blade.MoveTo(blade.SpawnX + ((blade.X - blade.SpawnX) * scale), blade.SpawnY + ((blade.Y - blade.SpawnY) * scale));
            return;
        }
    }
}
