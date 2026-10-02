namespace OpenGarrison.Core;

public sealed partial class MovementSystem
{
    public bool WouldRunIntoWall(PlayerEntity player, float moveDirection)
    {
        if (moveDirection == 0f)
        {
            return false;
        }

        var probeDistance = 18f;
        var probeLeft = moveDirection > 0f
            ? player.Right + probeDistance
            : player.Left - probeDistance;
        var probeRight = probeLeft + MathF.Sign(moveDirection) * 2f;
        if (probeRight < probeLeft)
        {
            (probeLeft, probeRight) = (probeRight, probeLeft);
        }

        var probeTop = player.Top;
        var probeBottom = player.Bottom - 4f;
        foreach (var solid in Level.Solids)
        {
            if (probeLeft < solid.Right
                && probeRight > solid.Left
                && probeTop < solid.Bottom
                && probeBottom > solid.Top)
            {
                return true;
            }
        }

        foreach (var gate in Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            var gateLeft = gate.Left;
            var gateRight = gate.Right;
            var gateTop = gate.Top;
            var gateBottom = gate.Bottom;
            if (probeLeft < gateRight
                && probeRight > gateLeft
                && probeTop < gateBottom
                && probeBottom > gateTop)
            {
                return true;
            }
        }

        foreach (var wall in Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            var wallLeft = wall.Left;
            var wallRight = wall.Right;
            var wallTop = wall.Top;
            var wallBottom = wall.Bottom;
            if (probeLeft < wallRight
                && probeRight > wallLeft
                && probeTop < wallBottom
                && probeBottom > wallTop)
            {
                if (wall.IsLeftDoor())
                {
                    if (moveDirection < 0f && player.Left >= wall.Right - 2f)
                    {
                        return true;
                    }

                    continue;
                }

                if (wall.IsRightDoor())
                {
                    if (moveDirection > 0f && player.Right <= wall.Left + 2f)
                    {
                        return true;
                    }

                    continue;
                }

                return true;
            }
        }

        return false;
    }

    public bool ApplyRoomForces(PlayerEntity player, bool jumpPressed = false)
    {
        EnsureCatapultContactCacheForCurrentLevel();

        if (!player.IsAlive)
        {
            ClearCatapultContactsForPlayer(player.Id);
            return false;
        }

        var consumedJumpPress = false;
        foreach (var index in Level.MoveBoxIndices)
        {
            ref readonly var roomObject = ref Level.GetRoomObject(index);

            if (!player.IntersectsMarker(
                roomObject.CenterX,
                roomObject.CenterY,
                roomObject.Width,
                roomObject.Height))
            {
                continue;
            }

            var impulse = roomObject.GetMoveBoxImpulse();
            if (impulse.X == 0f && impulse.Y == 0f)
            {
                continue;
            }

            player.SetMovementState(LegacyMovementState.None);
            player.AddImpulse(impulse.X, impulse.Y);
        }

        foreach (var index in Level.GetRoomObjectIndices(RoomObjectType.Catapult))
        {
            ref readonly var roomObject = ref Level.GetRoomObject(index);

            var intersects = player.IntersectsMarker(
                roomObject.CenterX,
                roomObject.CenterY,
                roomObject.Width,
                roomObject.Height);
            var key = (player.Id, index);
            var wasIntersecting = _catapultContacts.TryGetValue(key, out var previous) && previous;
            if (intersects)
            {
                _catapultContacts[key] = true;
            }
            else
            {
                _catapultContacts.Remove(key);
            }

            if (!intersects)
            {
                continue;
            }

            var configuration = roomObject.Catapult;
            var shouldLaunch = configuration.RequiresJumpPress
                ? jumpPressed
                : !wasIntersecting;
            if (!shouldLaunch)
            {
                continue;
            }

            var impulse = configuration.GetImpulse();
            if (impulse.X == 0f && impulse.Y == 0f)
            {
                continue;
            }

            player.SetMovementState(LegacyMovementState.None);
            player.AddImpulse(impulse.X, impulse.Y);
            consumedJumpPress |= configuration.RequiresJumpPress;
        }

        return consumedJumpPress;
    }

    public void EnsureCatapultContactCacheForCurrentLevel()
    {
        if (ReferenceEquals(_catapultContactLevel, Level))
        {
            return;
        }

        _catapultContacts.Clear();
        _catapultContactLevel = Level;
    }

    private void ClearCatapultContactsForPlayer(int playerId)
    {
        foreach (var index in Level.GetRoomObjectIndices(RoomObjectType.Catapult))
        {
            _catapultContacts.Remove((playerId, index));
        }
    }
}
