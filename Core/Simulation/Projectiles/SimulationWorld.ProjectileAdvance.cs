namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void RemoveShotAt(int shotIndex)
    {
        var shot = _shots[shotIndex];
        _entities.Remove(shot.Id);
        _shots.RemoveAt(shotIndex);
    }

    private void RemoveBladeAt(int bladeIndex)
    {
        var blade = _blades[bladeIndex];
        if (FindPlayerById(blade.OwnerId) is { } owner)
        {
            owner.DecrementQuoteBladeCount();
        }

        _entities.Remove(blade.Id);
        _blades.RemoveAt(bladeIndex);
    }

    private void RemoveNeedleAt(int needleIndex)
    {
        var needle = _needles[needleIndex];
        _entities.Remove(needle.Id);
        _needles.RemoveAt(needleIndex);
    }

    private void RemoveRevolverShotAt(int shotIndex)
    {
        var shot = _revolverShots[shotIndex];
        _entities.Remove(shot.Id);
        _revolverShots.RemoveAt(shotIndex);
    }

    private void RemoveOwnedSentries(int ownerId)
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (_sentries[sentryIndex].OwnerPlayerId == ownerId)
            {
                DestroySentry(_sentries[sentryIndex], attacker: null);
            }
        }
    }

    private void RemoveOwnedMines(int ownerId)
    {
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            if (_mines[mineIndex].OwnerId == ownerId)
            {
                RemoveMineAt(mineIndex);
            }
        }
    }

    private void RemoveOwnedProjectiles(int ownerId)
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_shots[shotIndex].OwnerId == ownerId)
            {
                RemoveShotAt(shotIndex);
            }
        }

        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_needles[needleIndex].OwnerId == ownerId)
            {
                RemoveNeedleAt(needleIndex);
            }
        }

        for (var shotIndex = _revolverShots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_revolverShots[shotIndex].OwnerId == ownerId)
            {
                RemoveRevolverShotAt(shotIndex);
            }
        }

        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (_bubbles[bubbleIndex].OwnerId == ownerId)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }

        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            if (_blades[bladeIndex].OwnerId == ownerId)
            {
                RemoveBladeAt(bladeIndex);
            }
        }

        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            if (_rockets[rocketIndex].OwnerId == ownerId)
            {
                RemoveRocketAt(rocketIndex);
            }
        }

        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            if (_flames[flameIndex].OwnerId == ownerId)
            {
                RemoveFlameAt(flameIndex);
            }
        }

        for (var flareIndex = _flares.Count - 1; flareIndex >= 0; flareIndex -= 1)
        {
            if (_flares[flareIndex].OwnerId == ownerId)
            {
                RemoveFlareAt(flareIndex);
            }
        }
    }

    private int CountOwnedMines(int ownerId)
    {
        var count = 0;
        foreach (var mine in _mines)
        {
            if (mine.OwnerId == ownerId)
            {
                count += 1;
            }
        }

        return count;
    }

    private static bool CircleIntersectsPlayer(float circleX, float circleY, float radius, PlayerEntity player)
    {
        player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        return CircleIntersectsRectangle(
            circleX,
            circleY,
            radius,
            left,
            top,
            right,
            bottom);
    }

    private static bool CircleIntersectsRectangle(float circleX, float circleY, float radius, float left, float top, float right, float bottom)
    {
        var closestX = float.Clamp(circleX, left, right);
        var closestY = float.Clamp(circleY, top, bottom);
        var deltaX = circleX - closestX;
        var deltaY = circleY - closestY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }
}
