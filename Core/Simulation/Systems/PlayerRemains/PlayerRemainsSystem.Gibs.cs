namespace OpenGarrison.Core;

internal sealed partial class PlayerRemainsSystem
{
    private int AllocateGoreEntityId(bool clientOnly = false)
        => clientOnly || _host.ClientPredictionMode
            ? _host.EntityStore.AllocateLocalEffectId()
            : _host.AllocateEntityId();

    private readonly record struct PlayerGibPartDefinition(
        string SpriteName,
        int FrameIndex,
        int Count,
        float VelocityRangeX,
        float VelocityRangeY,
        float RotationRange,
        int LifetimeTicks,
        float HorizontalFriction,
        float RotationFriction,
        bool InheritPlayerVelocity = false,
        float BloodChance = PlayerGibEntity.DefaultBloodChance);

    internal void SpawnPlayerGibs(PlayerEntity player)
    {
        var experimentalCryoTinted = player.IsExperimentalCryoFrozen;
        if (!player.IsAlive || SimulationConstants.DefaultGibLevel <= 1)
        {
            _host.PlayerDeaths.SpawnDeadBody(player);
            return;
        }

        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var inheritedVelocityX = player.HorizontalSpeed * (float)_host.Config.FixedDeltaSeconds;
        var inheritedVelocityY = player.VerticalSpeed * (float)_host.Config.FixedDeltaSeconds;
        var hasAuthoredParts = AuthoredPlayerGibCatalog.TryGetParts(player.GameplayClassId, player.Team, out var authoredParts);
        if (!hasAuthoredParts)
        {
            SpawnPlayerGibSet(player, "GibS", SimulationConstants.DefaultGibLevel, randomFrameCount: 7, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 210, horizontalFriction: 0.4f, rotationFriction: 0.6f, bloodChance: 1.8f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY, experimentalCryoTinted: experimentalCryoTinted, emitNetworkEvents: false);
            SpawnPlayerGibSet(player, player.Team == PlayerTeam.Blue ? "BlueClumpS" : "RedClumpS", SimulationConstants.DefaultGibLevel - 1, randomFrameCount: 4, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 250, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 2f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY, experimentalCryoTinted: experimentalCryoTinted, emitNetworkEvents: false);
        }

        _host.WorldEffects.RegisterVisualEffect("GibBlood", player.X, player.Y, count: SimulationConstants.DefaultGibLevel);
        SpawnBloodDrops(player.X, player.Y, SimulationConstants.DefaultGibLevel * 14, 10f, 13f, spreadRadius: 11f, experimentalCryoTinted: experimentalCryoTinted);

        if (hasAuthoredParts)
        {
            SpawnAuthoredPlayerGibs(player, authoredParts, inheritedVelocityX, inheritedVelocityY, experimentalCryoTinted, emitNetworkEvents: false);
            return;
        }

        foreach (var gibPart in GetPlayerGibParts(player))
        {
            SpawnPlayerGibSet(
                player,
                gibPart.SpriteName,
                gibPart.Count,
                frameIndex: gibPart.FrameIndex,
                velocityRangeX: gibPart.VelocityRangeX,
                velocityRangeY: gibPart.VelocityRangeY,
                rotationRange: gibPart.RotationRange,
                lifetimeTicks: gibPart.LifetimeTicks,
                horizontalFriction: gibPart.HorizontalFriction,
                rotationFriction: gibPart.RotationFriction,
                bloodChance: gibPart.BloodChance,
                inheritedVelocityX: gibPart.InheritPlayerVelocity ? inheritedVelocityX : 0f,
                inheritedVelocityY: gibPart.InheritPlayerVelocity ? inheritedVelocityY : 0f,
                experimentalCryoTinted: experimentalCryoTinted,
                emitNetworkEvents: false);
        }
    }

    private void SpawnPlayerGibsForNetworkDeath(PlayerEntity player, bool clientOnly, float? spawnX = null, float? spawnY = null)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var inheritedVelocityX = player.HorizontalSpeed * (float)_host.Config.FixedDeltaSeconds;
        var inheritedVelocityY = player.VerticalSpeed * (float)_host.Config.FixedDeltaSeconds;
        var hasAuthoredParts = AuthoredPlayerGibCatalog.TryGetParts(player.GameplayClassId, player.Team, out var authoredParts);
        if (!hasAuthoredParts)
        {
            SpawnPlayerGibSet(player, "GibS", SimulationConstants.DefaultGibLevel, randomFrameCount: 7, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 210, horizontalFriction: 0.4f, rotationFriction: 0.6f, bloodChance: 1.8f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY, emitNetworkEvents: false, spawnX: spawnX, spawnY: spawnY, clientOnly: clientOnly);
            SpawnPlayerGibSet(player, player.Team == PlayerTeam.Blue ? "BlueClumpS" : "RedClumpS", SimulationConstants.DefaultGibLevel - 1, randomFrameCount: 4, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 250, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 2f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY, emitNetworkEvents: false, spawnX: spawnX, spawnY: spawnY, clientOnly: clientOnly);
        }

        if (hasAuthoredParts)
        {
            SpawnAuthoredPlayerGibs(
                player,
                authoredParts,
                inheritedVelocityX,
                inheritedVelocityY,
                player.IsExperimentalCryoFrozen,
                emitNetworkEvents: false,
                spawnX: spawnX,
                spawnY: spawnY,
                clientOnly: clientOnly);
            return;
        }

        foreach (var gibPart in GetPlayerGibParts(player))
        {
            SpawnPlayerGibSet(
                player,
                gibPart.SpriteName,
                gibPart.Count,
                frameIndex: gibPart.FrameIndex,
                velocityRangeX: gibPart.VelocityRangeX,
                velocityRangeY: gibPart.VelocityRangeY,
                rotationRange: gibPart.RotationRange,
                lifetimeTicks: gibPart.LifetimeTicks,
                horizontalFriction: gibPart.HorizontalFriction,
                rotationFriction: gibPart.RotationFriction,
                bloodChance: gibPart.BloodChance,
                inheritedVelocityX: gibPart.InheritPlayerVelocity ? inheritedVelocityX : 0f,
                inheritedVelocityY: gibPart.InheritPlayerVelocity ? inheritedVelocityY : 0f,
                emitNetworkEvents: false,
                spawnX: spawnX,
                spawnY: spawnY,
                clientOnly: clientOnly);
        }
    }

    internal void SpawnClientPlayerGibsFromNetworkDeath(PlayerEntity player, float? spawnX = null, float? spawnY = null)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var resolvedSpawnX = spawnX ?? player.X;
        var resolvedSpawnY = spawnY ?? player.Y;
        SpawnPlayerGibsForNetworkDeath(player, clientOnly: true, spawnX: resolvedSpawnX, spawnY: resolvedSpawnY);
        _host.WorldEffects.RegisterVisualEffect("GibBlood", resolvedSpawnX, resolvedSpawnY, count: SimulationConstants.DefaultGibLevel);
        SpawnBloodDrops(resolvedSpawnX, resolvedSpawnY, SimulationConstants.DefaultGibLevel * 14, 10f, 13f, spreadRadius: 11f, experimentalCryoTinted: player.IsExperimentalCryoFrozen, clientOnly: true);
    }

    private void SpawnAuthoredPlayerGibs(
        PlayerEntity player,
        IReadOnlyList<AuthoredPlayerGibPart> parts,
        float inheritedVelocityX,
        float inheritedVelocityY,
        bool experimentalCryoTinted,
        bool emitNetworkEvents,
        float? spawnX = null,
        float? spawnY = null,
        bool clientOnly = false)
    {
        var originX = spawnX ?? player.X;
        var originY = spawnY ?? player.Y;
        var facingLeft = player.FacingDirectionX < 0f;
        foreach (var part in parts)
        {
            SpawnPlayerGibSet(
                player,
                part.SpriteName,
                count: 1,
                frameIndex: 0,
                velocityRangeX: part.VelocityRangeX,
                velocityRangeY: part.VelocityRangeY,
                rotationRange: part.RotationRange,
                lifetimeTicks: 250,
                horizontalFriction: part.HorizontalFriction,
                rotationFriction: part.RotationFriction,
                bloodChance: part.BloodChance,
                inheritedVelocityX: part.InheritPlayerVelocity ? inheritedVelocityX : 0f,
                inheritedVelocityY: part.InheritPlayerVelocity ? inheritedVelocityY : 0f,
                experimentalCryoTinted: experimentalCryoTinted,
                emitNetworkEvents: emitNetworkEvents,
                spawnX: originX + (part.SpawnOffsetX * player.PlayerScale * (facingLeft ? -1f : 1f)),
                spawnY: originY + (part.SpawnOffsetY * player.PlayerScale),
                flipHorizontally: facingLeft,
                authoredRenderScale: 2f * player.PlayerScale,
                clientOnly: clientOnly);
        }
    }

    private void SpawnPlayerGibSet(
        PlayerEntity player,
        string spriteName,
        int count,
        int? frameIndex = null,
        int randomFrameCount = 0,
        float velocityRangeX = 8f,
        float velocityRangeY = 9f,
        float rotationRange = 52f,
        int lifetimeTicks = 250,
        float horizontalFriction = 0.4f,
        float rotationFriction = 0.5f,
        float bloodChance = PlayerGibEntity.DefaultBloodChance,
        float inheritedVelocityX = 0f,
        float inheritedVelocityY = 0f,
        bool experimentalCryoTinted = false,
        bool emitNetworkEvents = true,
        float? spawnX = null,
        float? spawnY = null,
        bool flipHorizontally = false,
        float authoredRenderScale = 2f,
        bool clientOnly = false)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var resolvedSpawnX = spawnX ?? player.X;
        var resolvedSpawnY = spawnY ?? player.Y;
        for (var index = 0; index < count; index += 1)
        {
            var resolvedFrameIndex = frameIndex ?? _host.Randoms.Gameplay.Next(randomFrameCount);
            var velocityX = inheritedVelocityX + ((_host.Randoms.Gameplay.NextSingle() * ((velocityRangeX * 2f) + 1f)) - velocityRangeX);
            var velocityY = inheritedVelocityY + ((_host.Randoms.Gameplay.NextSingle() * ((velocityRangeY * 2f) + 1f)) - velocityRangeY);
            if (_host.Level.IsTopDown)
            {
                var angle = _host.Randoms.Gameplay.NextSingle() * (MathF.PI * 2f);
                var radialSpeed = MathF.Max(2f, MathF.Max(velocityRangeX, velocityRangeY) * (0.45f + (_host.Randoms.Gameplay.NextSingle() * 0.55f)));
                velocityX = inheritedVelocityX + (DeterministicMath.Cos(angle) * radialSpeed);
                velocityY = inheritedVelocityY + (DeterministicMath.Sin(angle) * radialSpeed);
            }
            var rotationSpeed = (_host.Randoms.Gameplay.NextSingle() * ((rotationRange * 2f) + 1f)) - rotationRange;

            // Create gib entity locally (for offline mode and server-side simulation)
            var gib = new PlayerGibEntity(
                AllocateGoreEntityId(clientOnly),
                spriteName,
                resolvedFrameIndex,
                resolvedSpawnX,
                resolvedSpawnY,
                velocityX,
                velocityY,
                rotationSpeed,
                horizontalFriction,
                rotationFriction,
                lifetimeTicks,
                bloodChance,
                experimentalCryoTinted,
                flipHorizontally,
                authoredRenderScale);
            _host.WorldObjects.PlayerGibs.Add(gib);
            _host.EntityStore.Add(gib);

            if (emitNetworkEvents)
            {
                // Emit event for network replication to clients.
                _host.PresentationEvents.AddGibSpawnEvent(new WorldGibSpawnEvent(
                    spriteName,
                    resolvedFrameIndex,
                    resolvedSpawnX,
                    resolvedSpawnY,
                    velocityX,
                    velocityY,
                    rotationSpeed,
                    horizontalFriction,
                    rotationFriction,
                    lifetimeTicks,
                    bloodChance));
            }
        }
    }

    internal void TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity victim, float launchDirectionX, float launchDirectionY)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var headSpriteName = ExperimentalDemoknightCatalog.GetDecapitatedHeadSpriteName(victim.ClassId, victim.Team);
        if (string.IsNullOrWhiteSpace(headSpriteName))
        {
            return;
        }

        var directionLength = MathF.Sqrt((launchDirectionX * launchDirectionX) + (launchDirectionY * launchDirectionY));
        var normalizedDirectionX = directionLength <= 0.0001f ? 1f : launchDirectionX / directionLength;
        var normalizedDirectionY = directionLength <= 0.0001f ? 0f : launchDirectionY / directionLength;
        var spawnX = victim.X;
        var spawnY = victim.Y - (victim.Height * 0.42f);
        var velocityX = (normalizedDirectionX * 6f) + ((_host.Randoms.Gameplay.NextSingle() * 4f) - 2f);
        var velocityY = (normalizedDirectionY * 2.5f) - 6f - (_host.Randoms.Gameplay.NextSingle() * 2f);
        var rotationSpeed = (_host.Randoms.Gameplay.NextSingle() * 160f) - 80f;

        // Create gib entity locally (for offline mode and server-side simulation)
        var headGib = new PlayerGibEntity(
            AllocateGoreEntityId(),
            headSpriteName,
            frameIndex: 0,
            spawnX,
            spawnY,
            velocityX,
            velocityY,
            rotationSpeed,
            horizontalFriction: 0.55f,
            rotationFriction: 0.55f,
            lifetimeTicks: 250,
            bloodChance: 1.3f);
        _host.WorldObjects.PlayerGibs.Add(headGib);
        _host.EntityStore.Add(headGib);

        // Emit event for network replication to clients
        _host.PresentationEvents.AddGibSpawnEvent(new WorldGibSpawnEvent(
            headSpriteName,
            0,
            spawnX,
            spawnY,
            velocityX,
            velocityY,
            rotationSpeed,
            0.55f,
            0.55f,
            250,
            1.3f));

        _host.WorldEffects.RegisterVisualEffect("GibBlood", spawnX, spawnY, count: 1);
        SpawnBloodDrops(spawnX, spawnY, 18, 8f, 12f, spreadRadius: 6f);
    }

    private static IEnumerable<PlayerGibPartDefinition> GetPlayerGibParts(PlayerEntity player)
    {
        switch (player.ClassId)
        {
            case PlayerClass.Scout:
                yield return new PlayerGibPartDefinition("HeadS", 6, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 0, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 1, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Pyro:
                yield return new PlayerGibPartDefinition("HeadS", 7, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("AccesoryS", 4, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                yield return new PlayerGibPartDefinition("FeetS", 1, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Soldier:
                yield return new PlayerGibPartDefinition("HeadS", 1, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 2, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 1, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                yield return new PlayerGibPartDefinition("AccesoryS", player.Team == PlayerTeam.Blue ? 2 : 1, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                break;
            case PlayerClass.Heavy:
                yield return new PlayerGibPartDefinition("HeadS", 2, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 3, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 1, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Demoman:
                yield return new PlayerGibPartDefinition("HeadS", 4, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 4, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Medic:
                yield return new PlayerGibPartDefinition("HeadS", 5, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 4, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", player.Team == PlayerTeam.Blue ? 3 : 2, 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Engineer:
                yield return new PlayerGibPartDefinition("HeadS", 8, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("AccesoryS", 3, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                yield return new PlayerGibPartDefinition("FeetS", 5, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Spy:
                yield return new PlayerGibPartDefinition("HeadS", 3, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 6, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Sniper:
                yield return new PlayerGibPartDefinition("HeadS", 0, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("AccesoryS", 0, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                yield return new PlayerGibPartDefinition("FeetS", 6, SimulationConstants.DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, SimulationConstants.DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
        }
    }

    internal void AdvancePlayerGibs()
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            _host.WorldObjects.RemoveAll(_host.WorldObjects.PlayerGibs);
            return;
        }

        for (var gibIndex = _host.WorldObjects.PlayerGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _host.WorldObjects.PlayerGibs[gibIndex];
            var velocityXBeforeAdvance = gib.VelocityX;
            var velocityYBeforeAdvance = gib.VelocityY;
            gib.Advance(_host.Level, _host.Level.Bounds, topDown: _host.Level.IsTopDown);
            TryPlayPlayerGibLandingSplat(gib, velocityXBeforeAdvance, velocityYBeforeAdvance);
            TryApplyPlayerGibSplat(gib);
            TrySpawnBloodDropFromGib(gib);
            if (!gib.IsExpired)
            {
                continue;
            }

            _host.EntityStore.RemoveIfSame(gib);
            _host.WorldObjects.PlayerGibs.RemoveAt(gibIndex);
        }
    }

    // GG2 Gib "Collision with Obstacle": a gib that hits the ground with
    // speed > 4 and vspeed > 2 plays Splat (and throws blood, drawn client-side).
    private const float PlayerGibLandingSplatMinimumSpeed = 4f;
    private const float PlayerGibLandingSplatMinimumFallSpeed = 2f;
    // GG2 Gib "Collision with Character": kicked only by a player running
    // faster than 3 px per source step, with a 2-in-9 chance each step.
    private const float PlayerGibKickMinimumRunSpeedPerSourceStep = 3f;
    private const float PlayerGibKickChance = 2f / 9f;
    // GG2 used hspeed += 1.2 * run speed, vspeed -= 2.7 and a spin of up to
    // +/-58 degrees per px/step of run speed. That reads as too violent here,
    // so the kick is scaled down; the GG2 values are noted next to each one.
    private const float PlayerGibKickHorizontalScale = 0.6f; // GG2: 1.2
    private const float PlayerGibKickUpwardSpeed = 1.6f; // GG2: 2.7
    private const float PlayerGibKickMaxSpinPerRunSpeed = 6f; // GG2: 0.8 * 73

    private void TryPlayPlayerGibLandingSplat(PlayerGibEntity gib, float velocityXBeforeAdvance, float velocityYBeforeAdvance)
    {
        if (_host.Level.IsTopDown
            || gib.IsExpired
            || velocityYBeforeAdvance <= PlayerGibLandingSplatMinimumFallSpeed
            || gib.VelocityY > 0f)
        {
            return;
        }

        var impactSpeed = MathF.Sqrt((velocityXBeforeAdvance * velocityXBeforeAdvance) + (velocityYBeforeAdvance * velocityYBeforeAdvance));
        if (impactSpeed > PlayerGibLandingSplatMinimumSpeed)
        {
            _host.WorldEffects.RegisterWorldSoundEvent("Splat", gib.X, gib.Y);
        }
    }

    private void TryApplyPlayerGibSplat(PlayerGibEntity gib)
    {
        if (gib.IsExpired)
        {
            return;
        }

        if (!_host.Level.IsTopDown)
        {
            TryApplyPlayerGibKick(gib);
            return;
        }

        if (!gib.CanSplat)
        {
            return;
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || !gib.IntersectsPlayer(player))
            {
                continue;
            }

            var impulseX = player.HorizontalSpeed * (float)_host.Config.FixedDeltaSeconds;
            var impulseY = player.VerticalSpeed * (float)_host.Config.FixedDeltaSeconds;
            if (MathF.Abs(impulseX) < 0.25f && MathF.Abs(impulseY) < 0.25f)
            {
                impulseX = MathF.Sign(player.FacingDirectionX == 0f ? 1f : player.FacingDirectionX) * 2.2f;
            }

            impulseX = Math.Clamp(impulseX * 0.8f, -5.5f, 5.5f);
            impulseY = Math.Clamp(impulseY * 0.45f, -3.5f, 2.5f);
            gib.AddImpulse(impulseX, impulseY, impulseX * 8f);
            gib.RestartSplatCooldown();
            _host.WorldEffects.RegisterWorldSoundEvent("Splat", gib.X, gib.Y);
            return;
        }
    }

    private void TryApplyPlayerGibKick(PlayerGibEntity gib)
    {
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            // GG2 skips fully cloaked spies (cloakAlpha == 0).
            if (!player.IsAlive || player.SpyCloakAlpha <= 0f || !gib.IntersectsPlayer(player))
            {
                continue;
            }

            // GG2 only checked other.hspeed > 3, so only rightward runs kicked;
            // either direction counts here.
            var runSpeedPerSourceStep = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
            if (MathF.Abs(runSpeedPerSourceStep) <= PlayerGibKickMinimumRunSpeedPerSourceStep
                || _host.Randoms.Gameplay.NextSingle() >= PlayerGibKickChance)
            {
                continue;
            }

            // A gib already flying ahead of the runner is not kicked again, so
            // overlapping for a few steps cannot stack several kicks.
            var kickSpeedX = runSpeedPerSourceStep * PlayerGibKickHorizontalScale;
            if (MathF.Sign(gib.VelocityX) == MathF.Sign(kickSpeedX)
                && MathF.Abs(gib.VelocityX) >= MathF.Abs(kickSpeedX))
            {
                continue;
            }

            gib.AddImpulse(
                kickSpeedX,
                -PlayerGibKickUpwardSpeed,
                runSpeedPerSourceStep * PlayerGibKickMaxSpinPerRunSpeed * ((_host.Randoms.Gameplay.NextSingle() * 2f) - 1f));
        }
    }

    internal void AdvanceBloodDrops()
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            _host.WorldObjects.RemoveAll(_host.WorldObjects.BloodDrops);
            return;
        }

        for (var dropIndex = _host.WorldObjects.BloodDrops.Count - 1; dropIndex >= 0; dropIndex -= 1)
        {
            var bloodDrop = _host.WorldObjects.BloodDrops[dropIndex];
            bloodDrop.Advance(_host.Level, _host.Level.Bounds, topDown: _host.Level.IsTopDown);
            if (!bloodDrop.IsExpired)
            {
                continue;
            }

            _host.EntityStore.RemoveIfSame(bloodDrop);
            _host.WorldObjects.BloodDrops.RemoveAt(dropIndex);
        }

        MergeBloodDrops();
    }

    private void TrySpawnBloodDropFromGib(PlayerGibEntity gib)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        if (gib.IsExpired || gib.BloodChance <= 0f)
        {
            return;
        }

        var threshold = 16f / SimulationConstants.DefaultGibLevel;
        if (MathF.Abs(gib.Speed / gib.BloodChance) <= _host.Randoms.Gameplay.NextSingle() * threshold)
        {
            return;
        }

        var angle = DeterministicMath.Atan2(gib.VelocityY, gib.VelocityX);
        var bloodDrop = new BloodDropEntity(
            AllocateGoreEntityId(clientOnly: gib.Id < 0),
            gib.X,
            gib.Y - 1f,
            DeterministicMath.Cos(angle) * gib.Speed * 0.9f + (_host.Randoms.Gameplay.NextSingle() * 3f) - 1f,
            DeterministicMath.Sin(angle) * gib.Speed * 0.9f + (_host.Randoms.Gameplay.NextSingle() * 3f) - 1f,
            experimentalCryoTinted: gib.ExperimentalCryoTinted,
            lifetimeTicks: _host.ScaleBloodDropLifetimeTicks());
        _host.WorldObjects.BloodDrops.Add(bloodDrop);
        _host.EntityStore.Add(bloodDrop);
    }

    private void SpawnBloodDrops(float x, float y, int count, float velocityRangeX, float velocityRangeY, float spreadRadius = 0f, bool experimentalCryoTinted = false, bool clientOnly = false)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var lifetimeTicks = _host.ScaleBloodDropLifetimeTicks();
        for (var index = 0; index < count; index += 1)
        {
            var offsetX = spreadRadius <= 0f ? 0f : (_host.Randoms.Gameplay.NextSingle() * ((spreadRadius * 2f) + 1f)) - spreadRadius;
            var offsetY = spreadRadius <= 0f ? 0f : (_host.Randoms.Gameplay.NextSingle() * ((spreadRadius * 2f) + 1f)) - spreadRadius;
            var velocityX = (_host.Randoms.Gameplay.NextSingle() * ((velocityRangeX * 2f) + 1f)) - velocityRangeX;
            var velocityY = (_host.Randoms.Gameplay.NextSingle() * ((velocityRangeY * 2f) + 1f)) - velocityRangeY;
            var bloodDrop = new BloodDropEntity(
                AllocateGoreEntityId(clientOnly),
                x + offsetX,
                y + offsetY,
                velocityX,
                velocityY,
                experimentalCryoTinted: experimentalCryoTinted,
                lifetimeTicks: lifetimeTicks);
            _host.WorldObjects.BloodDrops.Add(bloodDrop);
            _host.EntityStore.Add(bloodDrop);
        }
    }

    /// <summary>
    /// Spawns blood client-side based on damage events from the server.
    /// </summary>
    internal void SpawnClientBloodFromDamage(float x, float y, int damageAmount)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        if (damageAmount <= 0)
        {
            return;
        }

        // Spawn blood proportional to damage (1 drop per 4 damage, max 8 drops)
        var bloodCount = Math.Min(8, Math.Max(1, damageAmount / 4));
        SpawnBloodDrops(x, y, bloodCount, velocityRangeX: 6f, velocityRangeY: 8f, spreadRadius: 3f, clientOnly: true);
    }


    private void MergeBloodDrops()
    {
        if (_host.WorldObjects.BloodDrops.Count < 2)
        {
            return;
        }

        const float bucketSize = (BloodDropEntity.MaxScale * 2f) + 0.5f;
        var buckets = new Dictionary<long, List<int>>();
        bool[]? absorbedDrops = null;

        for (var sourceIndex = 0; sourceIndex < _host.WorldObjects.BloodDrops.Count; sourceIndex += 1)
        {
            var source = _host.WorldObjects.BloodDrops[sourceIndex];
            if (!source.IsMergeable)
            {
                continue;
            }

            var sourceCellX = GetBloodDropMergeCell(source.X, bucketSize);
            var sourceCellY = GetBloodDropMergeCell(source.Y, bucketSize);
            var absorbed = false;

            for (var offsetY = -1; offsetY <= 1 && !absorbed; offsetY += 1)
            {
                for (var offsetX = -1; offsetX <= 1 && !absorbed; offsetX += 1)
                {
                    var bucketKey = GetBloodDropMergeBucketKey(sourceCellX + offsetX, sourceCellY + offsetY);
                    if (!buckets.TryGetValue(bucketKey, out var targetIndices))
                    {
                        continue;
                    }

                    for (var targetBucketIndex = 0; targetBucketIndex < targetIndices.Count; targetBucketIndex += 1)
                    {
                        var target = _host.WorldObjects.BloodDrops[targetIndices[targetBucketIndex]];
                        if (!target.CanMergeWith(source) || !ShouldMergeBloodDrops(target, source))
                        {
                            continue;
                        }

                        target.Absorb(source);
                        _host.EntityStore.RemoveIfSame(source);
                        absorbedDrops ??= new bool[_host.WorldObjects.BloodDrops.Count];
                        absorbedDrops[sourceIndex] = true;
                        absorbed = true;
                        break;
                    }
                }
            }

            if (absorbed)
            {
                continue;
            }

            var sourceBucketKey = GetBloodDropMergeBucketKey(sourceCellX, sourceCellY);
            if (!buckets.TryGetValue(sourceBucketKey, out var sourceBucket))
            {
                sourceBucket = new List<int>();
                buckets.Add(sourceBucketKey, sourceBucket);
            }

            sourceBucket.Add(sourceIndex);
        }

        if (absorbedDrops is null)
        {
            return;
        }

        for (var dropIndex = _host.WorldObjects.BloodDrops.Count - 1; dropIndex >= 0; dropIndex -= 1)
        {
            if (absorbedDrops[dropIndex])
            {
                _host.WorldObjects.BloodDrops.RemoveAt(dropIndex);
            }
        }
    }

    private static int GetBloodDropMergeCell(float value, float bucketSize)
    {
        return (int)MathF.Floor(value / bucketSize);
    }

    private static long GetBloodDropMergeBucketKey(int cellX, int cellY)
    {
        return ((long)cellX << 32) ^ (uint)cellY;
    }

    private bool ShouldMergeBloodDrops(BloodDropEntity target, BloodDropEntity source)
    {
        return (((ulong)_host.Frame + (ulong)target.Id + (ulong)source.Id) & 7UL) == 0UL;
    }

    internal void AdvanceDeadBodies()
    {
        for (var deadBodyIndex = _host.WorldObjects.DeadBodies.Count - 1; deadBodyIndex >= 0; deadBodyIndex -= 1)
        {
            var deadBody = _host.WorldObjects.DeadBodies[deadBodyIndex];
            var advanceResult = deadBody.Advance(_host.Level, _host.Level.Bounds, preservePosition: _host.Level.IsTopDown);
            if (!_host.ClientPredictionMode
                && advanceResult.HitGround
                && advanceResult.ImpactSpeed >= 3.5f
                && deadBody.TryRestartImpactSoundCooldown())
            {
                _host.WorldEffects.RegisterWorldSoundEvent("ImpactSnd", deadBody.X, deadBody.Y);
            }

            if (!deadBody.IsExpired)
            {
                continue;
            }

            _host.EntityStore.Remove(deadBody.Id);
            _host.WorldObjects.DeadBodies.RemoveAt(deadBodyIndex);
        }
    }
}
