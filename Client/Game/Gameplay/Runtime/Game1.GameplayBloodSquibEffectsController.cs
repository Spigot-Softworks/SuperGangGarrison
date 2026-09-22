#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed partial class GameplayGoreEffectsController
    {
        private const float BloodCellSize = 2f;
        private const int MaxBloodSquibParticles = 96;
        private const int MaxSettledBloodCells = 2800;
        private const float SettledBloodMaxAmount = 2.4f;
        private const float SettledBloodDepositAmount = 1.05f;
        private const float SettledBloodDripThreshold = 1f;
        private const float SettledBloodDripRate = 0.004f;
        private const int SettledBloodFadeStartTicks = 700;
        private const int SettledBloodMaxAgeTicks = 1100;
        private const float SquibGravity = 0.27f;
        private const float SquibHeavyGravity = 0.34f;
        private const float SquibMaxSpeed = 12f;
        private const float SquibHeavyMaxSpeed = 9f;
        private const float SquibTrailScaleThreshold = 0.38f;
        private const float SquibGroundSnapMaxDistance = 420f;
        // Y-down angles: negative = toward world-up. Shot cone = 15° up + 5° down.
        private const float SquibShotSpreadUpDegrees = 15f;
        private const float SquibShotSpreadDownDegrees = 5f;

        public void ResetBloodSquibEffects()
        {
            _game._bloodSquibParticles.Clear();
            _game._settledBloodCells.Clear();
            _game._processedSettledBloodDropIds.Clear();
            _game._staleSettledBloodDropIds.Clear();
            _game._staleSettledBloodCellKeys.Clear();
            _game._pendingSettledBloodTransfers.Clear();
            _game._bloodDrawCellsScratch.Clear();
            _game._bloodCryoDrawCellsScratch.Clear();
        }

        private void AdvanceBloodSquibEffects()
        {
            // Floor stains come only from visible client squibs — not legacy BloodDropEntity.
            AdvanceBloodSquibParticles();
            AdvanceSettledBloodSeepage();
        }

        private void SpawnBloodSquibBurst(float x, float y, float directionDegrees, int burstCount, bool explosive = false)
        {
            if (!_game.AreBloodVisualsEnabled)
            {
                return;
            }

            var sprayCount = explosive
                ? _game.ScaleBloodVisualCount(Math.Clamp(Math.Max(8, burstCount * 2), 8, 16))
                : _game.ScaleBloodVisualCount(1);
            if (sprayCount <= 0)
            {
                return;
            }

            for (var index = 0; index < sprayCount; index += 1)
            {
                float spreadDegrees;
                if (explosive)
                {
                    // Full radial blast — ignore the downward placeholder direction.
                    spreadDegrees = _game._visualRandom.NextSingle() * 360f;
                }
                else
                {
                    // Focused along hit direction: 15° toward up, 5° toward down (20° total).
                    var offsetDegrees = -SquibShotSpreadUpDegrees
                        + (_game._visualRandom.NextSingle() * (SquibShotSpreadUpDegrees + SquibShotSpreadDownDegrees));
                    spreadDegrees = directionDegrees + offsetDegrees;
                }

                var spreadRadians = spreadDegrees * (MathF.PI / 180f);
                var speed = explosive
                    ? 5.2f + (_game._visualRandom.NextSingle() * 8f)
                    : 3.4f + (_game._visualRandom.NextSingle() * 3.2f);
                var scale = explosive
                    ? 0.22f + (_game._visualRandom.NextSingle() * 0.4f)
                    : 0.2f + (_game._visualRandom.NextSingle() * 0.35f);
                var spawnRadius = explosive
                    ? _game._visualRandom.NextSingle() * 4f
                    : _game._visualRandom.NextSingle() * 0.3f;
                TryAddBloodSquib(
                    x + MathF.Cos(spreadRadians) * spawnRadius,
                    y + MathF.Sin(spreadRadians) * spawnRadius,
                    MathF.Cos(spreadRadians) * speed,
                    MathF.Sin(spreadRadians) * speed,
                    scale,
                    explosive ? _game._visualRandom.Next(28, 50) : _game._visualRandom.Next(22, 36),
                    experimentalCryoTinted: false,
                    heavy: false);
            }
        }

        private void SpawnBloodSquibGibBurst(float x, float y, int intensity)
        {
            if (!_game.AreBloodVisualsEnabled)
            {
                return;
            }

            var sprayCount = _game.ScaleBloodVisualCount(Math.Clamp(10 + (intensity * 2), 10, 18));
            if (sprayCount <= 0)
            {
                return;
            }

            for (var index = 0; index < sprayCount; index += 1)
            {
                var spreadRadians = _game._visualRandom.NextSingle() * MathF.Tau;
                var speed = 4.6f + (_game._visualRandom.NextSingle() * 8.5f);
                var startRadius = _game._visualRandom.NextSingle() * 3.5f;
                var scale = 0.22f + (_game._visualRandom.NextSingle() * 0.4f);
                TryAddBloodSquib(
                    x + MathF.Cos(spreadRadians) * startRadius,
                    y + MathF.Sin(spreadRadians) * startRadius,
                    MathF.Cos(spreadRadians) * speed,
                    MathF.Sin(spreadRadians) * speed,
                    scale,
                    _game._visualRandom.Next(26, 48),
                    experimentalCryoTinted: false,
                    heavy: false);
            }
        }

        private void TryAddBloodSquib(
            float x,
            float y,
            float velocityX,
            float velocityY,
            float scale,
            int lifetimeTicks,
            bool experimentalCryoTinted,
            bool heavy)
        {
            if (_game._bloodSquibParticles.Count >= MaxBloodSquibParticles)
            {
                // Drop the oldest particle instead of scanning for the weakest.
                _game._bloodSquibParticles.RemoveAt(0);
            }

            _game._bloodSquibParticles.Add(new BloodSquibParticle(
                x,
                y,
                velocityX,
                velocityY,
                scale,
                unchecked(_game._nextBloodSquibSeed++),
                lifetimeTicks,
                experimentalCryoTinted,
                heavy));
        }

        private void SyncStuckBloodDropsIntoSettledCover()
        {
            var bloodDrops = _game._world.BloodDrops;
            for (var index = 0; index < bloodDrops.Count; index += 1)
            {
                var bloodDrop = bloodDrops[index];
                if (!bloodDrop.IsStuck || bloodDrop.IsExpired)
                {
                    continue;
                }

                if (!_game._processedSettledBloodDropIds.Add(bloodDrop.Id))
                {
                    continue;
                }

                DepositSettledBlood(
                    bloodDrop.X,
                    bloodDrop.Y,
                    SettledBloodDepositAmount * MathF.Min(1.2f, bloodDrop.Scale),
                    bloodDrop.ExperimentalCryoTinted);
            }

            if (_game._processedSettledBloodDropIds.Count == 0)
            {
                return;
            }

            // Cheap stale cleanup: rebuild only when the processed set grows large.
            if (_game._processedSettledBloodDropIds.Count < 64)
            {
                return;
            }

            _game._staleSettledBloodDropIds.Clear();
            foreach (var processedDropId in _game._processedSettledBloodDropIds)
            {
                var isActive = false;
                for (var bloodDropIndex = 0; bloodDropIndex < bloodDrops.Count; bloodDropIndex += 1)
                {
                    if (bloodDrops[bloodDropIndex].Id == processedDropId)
                    {
                        isActive = true;
                        break;
                    }
                }

                if (!isActive)
                {
                    _game._staleSettledBloodDropIds.Add(processedDropId);
                }
            }

            for (var index = 0; index < _game._staleSettledBloodDropIds.Count; index += 1)
            {
                _game._processedSettledBloodDropIds.Remove(_game._staleSettledBloodDropIds[index]);
            }

            _game._staleSettledBloodDropIds.Clear();
        }

        private void AdvanceBloodSquibParticles()
        {
            var level = _game._world.Level;
            var bounds = _game._world.Bounds;
            var isTopDown = level.IsTopDown;
            var solids = level.Solids;

            for (var index = _game._bloodSquibParticles.Count - 1; index >= 0; index -= 1)
            {
                var squib = _game._bloodSquibParticles[index];
                if (squib.TicksRemaining > 0)
                {
                    squib.TicksRemaining -= 1;
                }

                var previousX = squib.X;
                var previousY = squib.Y;
                var gravity = squib.Heavy ? SquibHeavyGravity : SquibGravity;
                var maxSpeed = squib.Heavy ? SquibHeavyMaxSpeed : SquibMaxSpeed;

                // After lifetime, force a fast drop so every squib reaches ground instead of freezing mid-air.
                if (squib.TicksRemaining <= 0)
                {
                    squib.VelocityX *= 0.82f;
                    if (!isTopDown)
                    {
                        squib.VelocityY = Math.Clamp(MathF.Max(squib.VelocityY, 2.4f) + gravity, -maxSpeed, maxSpeed);
                    }
                }
                else if (!isTopDown)
                {
                    squib.VelocityY = Math.Clamp(squib.VelocityY + gravity, -maxSpeed, maxSpeed);
                }

                squib.VelocityX = Math.Clamp(squib.VelocityX * (squib.Heavy ? 0.96f : 0.985f), -maxSpeed, maxSpeed);
                squib.X += squib.VelocityX;
                squib.Y += squib.VelocityY;

                if (TryResolveSquibSolidCollision(squib, solids, previousX, previousY, out var landed, out var impactX, out var impactY)
                    && landed)
                {
                    TryDepositSettledBloodOnGround(impactX, impactY, SettledBloodDepositAmount * squib.Scale, squib.ExperimentalCryoTinted);
                    _game._bloodSquibParticles.RemoveAt(index);
                    continue;
                }

                var clampedX = bounds.ClampX(squib.X, BloodCellSize);
                var clampedY = bounds.ClampY(squib.Y, BloodCellSize);
                if (clampedX != squib.X || clampedY != squib.Y)
                {
                    // Left the world — snap any remaining mass onto ground under the exit point.
                    TryDepositSettledBloodOnGround(
                        clampedX,
                        clampedY,
                        SettledBloodDepositAmount * squib.Scale * 0.7f,
                        squib.ExperimentalCryoTinted);
                    _game._bloodSquibParticles.RemoveAt(index);
                    continue;
                }

                // Safety: if we've been dropping for a long time with no ground, cull without mid-air stain.
                if (squib.TicksRemaining <= -180)
                {
                    TryDepositSettledBloodOnGround(
                        squib.X,
                        squib.Y,
                        SettledBloodDepositAmount * squib.Scale * 0.7f,
                        squib.ExperimentalCryoTinted);
                    _game._bloodSquibParticles.RemoveAt(index);
                }
            }
        }

        private static bool TryResolveSquibSolidCollision(
            BloodSquibParticle squib,
            IReadOnlyList<LevelSolid> solids,
            float previousX,
            float previousY,
            out bool landed,
            out float impactX,
            out float impactY)
        {
            landed = false;
            impactX = squib.X;
            impactY = squib.Y;
            const float halfSize = BloodCellSize * 0.5f;

            for (var solidIndex = 0; solidIndex < solids.Count; solidIndex += 1)
            {
                var solid = solids[solidIndex];
                var currentLeft = squib.X - halfSize;
                var currentRight = squib.X + halfSize;
                var currentTop = squib.Y - halfSize;
                var currentBottom = squib.Y + halfSize;
                if (currentLeft >= solid.Right
                    || currentRight <= solid.Left
                    || currentTop >= solid.Bottom
                    || currentBottom <= solid.Top)
                {
                    continue;
                }

                var previousLeft = previousX - halfSize;
                var previousRight = previousX + halfSize;
                var previousTop = previousY - halfSize;
                var previousBottom = previousY + halfSize;
                var overlapX = MathF.Min(currentRight, solid.Right) - MathF.Max(currentLeft, solid.Left);
                var overlapY = MathF.Min(currentBottom, solid.Bottom) - MathF.Max(currentTop, solid.Top);
                if (overlapX <= 0f || overlapY <= 0f)
                {
                    continue;
                }

                if (overlapX < overlapY)
                {
                    if (previousRight <= solid.Left)
                    {
                        squib.X = solid.Left - halfSize;
                    }
                    else if (previousLeft >= solid.Right)
                    {
                        squib.X = solid.Right + halfSize;
                    }

                    squib.VelocityX *= -0.12f;
                    impactX = squib.X;
                    impactY = squib.Y;
                    // Walls never count as ground — keep falling so stains don't float mid-air.
                    landed = false;
                    return true;
                }

                if (previousBottom <= solid.Top)
                {
                    squib.Y = solid.Top - halfSize;
                    impactX = squib.X;
                    impactY = squib.Y;
                    landed = true;
                    return true;
                }

                if (previousTop >= solid.Bottom)
                {
                    squib.Y = solid.Bottom + halfSize;
                    squib.VelocityY = MathF.Abs(squib.VelocityY) * 0.15f;
                    impactX = squib.X;
                    impactY = squib.Y;
                    return true;
                }

                squib.Y = solid.Top - halfSize;
                impactX = squib.X;
                impactY = squib.Y;
                landed = true;
                return true;
            }

            return false;
        }

        private void AdvanceSettledBloodSeepage()
        {
            if (_game._settledBloodCells.Count == 0)
            {
                return;
            }

            FadeSettledBloodFromEdges();

            var solids = _game._world.Level.Solids;
            _game._pendingSettledBloodTransfers.Clear();
            _game._staleSettledBloodCellKeys.Clear();

            foreach (var entry in _game._settledBloodCells)
            {
                var cell = entry.Value;

                // Only every other cell seeps each tick (hash stripe) to cut solid queries.
                if (((entry.Key.X + entry.Key.Y + cell.Age) & 1) != 0)
                {
                    continue;
                }

                var worldX = (entry.Key.X * BloodCellSize) + (BloodCellSize * 0.5f);
                var worldY = (entry.Key.Y * BloodCellSize) + (BloodCellSize * 0.5f);
                if (!TryFindSolidContainingPoint(solids, worldX, worldY, out var hostSolid)
                    && !TryFindSolidBelowCell(solids, worldX, worldY, BloodCellSize, out hostSolid))
                {
                    continue;
                }

                cell.DripProgress += SettledBloodDripRate * (0.35f + (cell.Amount * 0.55f));
                if (cell.DripProgress < SettledBloodDripThreshold)
                {
                    continue;
                }

                cell.DripProgress -= SettledBloodDripThreshold;
                var transferAmount = MathF.Min(cell.Amount * 0.1f, 0.22f);
                if (transferAmount <= 0.025f)
                {
                    continue;
                }

                var targetX = entry.Key.X;
                var targetY = entry.Key.Y + 1;
                var targetWorldX = (targetX * BloodCellSize) + (BloodCellSize * 0.5f);
                var targetWorldY = (targetY * BloodCellSize) + (BloodCellSize * 0.5f);

                // Limited by platform thickness: stop once the next cell leaves the solid.
                if (targetWorldX < hostSolid.Left
                    || targetWorldX >= hostSolid.Right
                    || targetWorldY < hostSolid.Top
                    || targetWorldY >= hostSolid.Bottom)
                {
                    continue;
                }

                cell.Amount -= transferAmount;
                _game._pendingSettledBloodTransfers.Add((targetX, targetY, transferAmount, cell.ExperimentalCryoTinted));
                if (cell.Amount <= 0.05f)
                {
                    _game._staleSettledBloodCellKeys.Add(entry.Key);
                }
            }

            for (var index = 0; index < _game._staleSettledBloodCellKeys.Count; index += 1)
            {
                _game._settledBloodCells.Remove(_game._staleSettledBloodCellKeys[index]);
            }

            _game._staleSettledBloodCellKeys.Clear();

            for (var index = 0; index < _game._pendingSettledBloodTransfers.Count; index += 1)
            {
                var transfer = _game._pendingSettledBloodTransfers[index];
                AddSettledBloodCellAmount(transfer.X, transfer.Y, transfer.Amount, transfer.Cryo);
            }

            _game._pendingSettledBloodTransfers.Clear();
            TrimSettledBloodCellsIfNeeded();
        }

        private void FadeSettledBloodFromEdges()
        {
            _game._staleSettledBloodCellKeys.Clear();
            _game._bloodBridgeScratch.Clear();

            // Age every cell, but only erode the silhouette so large pools shrink from the sides.
            foreach (var entry in _game._settledBloodCells)
            {
                entry.Value.Age += 1;
            }

            foreach (var entry in _game._settledBloodCells)
            {
                var cell = entry.Value;
                var (gx, gy) = entry.Key;

                if (cell.Amount <= 0.05f)
                {
                    _game._staleSettledBloodCellKeys.Add(entry.Key);
                    continue;
                }

                if (cell.Age < SettledBloodFadeStartTicks)
                {
                    continue;
                }

                if (!IsSettledBloodSilhouetteEdge(gx, gy))
                {
                    // Interior of a pool stays intact until the edge eats inward.
                    continue;
                }

                var fadeT = Math.Clamp(
                    (cell.Age - SettledBloodFadeStartTicks)
                    / (float)Math.Max(1, SettledBloodMaxAgeTicks - SettledBloodFadeStartTicks),
                    0f,
                    1f);
                // Edge cells dissolve; later ages dissolve faster so the front marches inward.
                var fadeFactor = MathHelper.Lerp(0.985f, 0.92f, fadeT);
                cell.Amount *= fadeFactor;

                if (cell.Amount <= 0.08f || cell.Age >= SettledBloodMaxAgeTicks)
                {
                    _game._staleSettledBloodCellKeys.Add(entry.Key);
                }
            }

            for (var index = 0; index < _game._staleSettledBloodCellKeys.Count; index += 1)
            {
                _game._settledBloodCells.Remove(_game._staleSettledBloodCellKeys[index]);
            }

            _game._staleSettledBloodCellKeys.Clear();
        }

        private bool IsSettledBloodSilhouetteEdge(int gx, int gy)
        {
            // 4-neighbour gap => silhouette edge of the pool sheet.
            if (!_game._settledBloodCells.ContainsKey((gx - 1, gy))
                || !_game._settledBloodCells.ContainsKey((gx + 1, gy))
                || !_game._settledBloodCells.ContainsKey((gx, gy - 1))
                || !_game._settledBloodCells.ContainsKey((gx, gy + 1)))
            {
                return true;
            }

            // Also treat very thin neighbouring amounts as edge so fade doesn't leave speck islands.
            if (!_game._settledBloodCells.TryGetValue((gx - 1, gy), out var left) || left.Amount < 0.18f
                || !_game._settledBloodCells.TryGetValue((gx + 1, gy), out var right) || right.Amount < 0.18f
                || !_game._settledBloodCells.TryGetValue((gx, gy - 1), out var up) || up.Amount < 0.18f
                || !_game._settledBloodCells.TryGetValue((gx, gy + 1), out var down) || down.Amount < 0.18f)
            {
                return true;
            }

            return false;
        }

        private void TryDepositSettledBloodOnGround(float worldX, float worldY, float amount, bool experimentalCryoTinted)
        {
            if (amount <= 0.01f)
            {
                return;
            }

            if (!TrySnapToGroundSurface(_game._world.Level.Solids, worldX, worldY, out var surfaceX, out var surfaceY))
            {
                return;
            }

            DepositSettledBlood(surfaceX, surfaceY, amount, experimentalCryoTinted);
        }

        private static bool TrySnapToGroundSurface(
            IReadOnlyList<LevelSolid> solids,
            float worldX,
            float worldY,
            out float surfaceX,
            out float surfaceY)
        {
            surfaceX = worldX;
            surfaceY = worldY;
            var bestTop = float.MaxValue;
            var found = false;

            for (var index = 0; index < solids.Count; index += 1)
            {
                var solid = solids[index];
                if (worldX < solid.Left || worldX >= solid.Right)
                {
                    continue;
                }

                // Prefer the nearest solid top at or below the droplet (or just above if already overlapping).
                if (solid.Top < worldY - BloodCellSize)
                {
                    continue;
                }

                if (solid.Top > worldY + SquibGroundSnapMaxDistance)
                {
                    continue;
                }

                if (solid.Top < bestTop)
                {
                    bestTop = solid.Top;
                    found = true;
                }
            }

            // If we're already inside a solid (spawned in geometry), use that solid's top.
            if (!found)
            {
                for (var index = 0; index < solids.Count; index += 1)
                {
                    var solid = solids[index];
                    if (worldX < solid.Left
                        || worldX >= solid.Right
                        || worldY < solid.Top
                        || worldY >= solid.Bottom)
                    {
                        continue;
                    }

                    if (solid.Top < bestTop)
                    {
                        bestTop = solid.Top;
                        found = true;
                    }
                }
            }

            // Fall-back: nearest solid top anywhere below within snap distance.
            if (!found)
            {
                for (var index = 0; index < solids.Count; index += 1)
                {
                    var solid = solids[index];
                    if (worldX < solid.Left || worldX >= solid.Right)
                    {
                        continue;
                    }

                    if (solid.Top < worldY)
                    {
                        continue;
                    }

                    var distance = solid.Top - worldY;
                    if (distance > SquibGroundSnapMaxDistance || solid.Top >= bestTop)
                    {
                        continue;
                    }

                    bestTop = solid.Top;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            surfaceX = worldX;
            surfaceY = bestTop - (BloodCellSize * 0.5f);
            return true;
        }

        private void DepositSettledBlood(float worldX, float worldY, float amount, bool experimentalCryoTinted)
        {
            if (amount <= 0.01f)
            {
                return;
            }

            // Every droplet leaves a readable pool — never a single lone pixel.
            amount = MathF.Max(amount, 0.7f);

            var solids = _game._world.Level.Solids;
            var centerGx = (int)MathF.Floor(worldX / BloodCellSize);
            var centerGy = (int)MathF.Floor(worldY / BloodCellSize);
            var existing = 0f;
            if (_game._settledBloodCells.TryGetValue((centerGx, centerGy), out var centerCell))
            {
                existing = centerCell.Amount;
            }

            var neighbourMass = existing;
            var neighbourCount = 1;
            for (var oy = -1; oy <= 1; oy += 1)
            {
                for (var ox = -1; ox <= 1; ox += 1)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    if (!_game._settledBloodCells.TryGetValue((centerGx + ox, centerGy + oy), out var neighbour))
                    {
                        continue;
                    }

                    neighbourMass += neighbour.Amount;
                    neighbourCount += 1;
                }
            }

            var accum = Math.Clamp((neighbourMass / neighbourCount) / SettledBloodMaxAmount, 0f, 1f);
            EvaluateSurfaceCornerBoost(solids, worldX, worldY, out var cornerBoost, out var wallPullX);

            // Flat surface stain that widens/deepens only with accumulation.
            var radiusX = MathHelper.Lerp(2.8f, 7.0f, accum);
            var radiusUp = MathHelper.Lerp(0.15f, 0.9f, accum);
            var radiusDown = MathHelper.Lerp(0.2f, 5.5f, accum * accum);
            var seed = unchecked(
                ((int)MathF.Round(worldX * 12.9898f) * 374761393)
                ^ ((int)MathF.Round(worldY * 78.233f) * 668265263));

            var minGX = (int)MathF.Floor((worldX - radiusX) / BloodCellSize);
            var maxGX = (int)MathF.Floor((worldX + radiusX) / BloodCellSize);
            var minGY = (int)MathF.Floor((worldY - radiusUp) / BloodCellSize);
            var maxGY = (int)MathF.Floor((worldY + radiusDown) / BloodCellSize);

            // Center only — sides taper through the ellipse on the 2px grid.
            AddSettledBloodCellAmount(centerGx, centerGy, amount, experimentalCryoTinted);

            // One extra cell toward a wall/step riser — just enough to kill stair striping.
            if (cornerBoost > 0.25f && MathF.Abs(wallPullX) > 0.01f)
            {
                AddSettledBloodCellAmount(
                    centerGx + (int)MathF.Sign(wallPullX),
                    centerGy,
                    amount * 0.55f,
                    experimentalCryoTinted);
            }

            for (var gy = minGY; gy <= maxGY; gy += 1)
            {
                var cellCY = (gy * BloodCellSize) + (BloodCellSize * 0.5f);
                for (var gx = minGX; gx <= maxGX; gx += 1)
                {
                    if (gx == centerGx && gy == centerGy)
                    {
                        continue;
                    }

                    var cellCX = (gx * BloodCellSize) + (BloodCellSize * 0.5f);
                    var dx = (cellCX - worldX) / radiusX;
                    var localY = cellCY - worldY;
                    var columnNoise = GetBloodEdgeNoiseSample(gx, centerGy, seed);
                    var localRadiusDown = radiusDown * MathHelper.Lerp(0.35f, 1.4f, columnNoise);
                    var dy = localY < 0f
                        ? localY / MathF.Max(0.12f, radiusUp)
                        : localY / MathF.Max(0.2f, localRadiusDown);
                    var ellipse = (dx * dx * 1.35f) + (dy * dy);
                    if (ellipse > 1f)
                    {
                        continue;
                    }

                    var falloff = 1f - ellipse;
                    falloff *= falloff;

                    if (localY > 0f)
                    {
                        var fringeNoise = GetBloodEdgeNoiseSample(gx, gy, seed ^ unchecked((int)0xB100Du));
                        var depthCap = localRadiusDown * MathHelper.Lerp(0.25f, 1.05f, fringeNoise);
                        if (localY > depthCap)
                        {
                            continue;
                        }

                        var bottomT = localY / MathF.Max(0.01f, localRadiusDown);
                        falloff *= Math.Clamp(1.1f - (bottomT * 0.75f) + ((fringeNoise - 0.5f) * 0.55f), 0.1f, 1f);
                        if (falloff < 0.16f)
                        {
                            continue;
                        }
                    }

                    var massScale = MathHelper.Lerp(0.35f, 0.9f, accum);
                    AddSettledBloodCellAmount(gx, gy, amount * massScale * falloff, experimentalCryoTinted);
                }
            }

            BlendTowardNearbyPools(centerGx, centerGy, amount * 0.4f, experimentalCryoTinted);

            TrimSettledBloodCellsIfNeeded();
        }

        private static void EvaluateSurfaceCornerBoost(
            IReadOnlyList<LevelSolid> solids,
            float worldX,
            float worldY,
            out float cornerBoost,
            out float wallPullX)
        {
            cornerBoost = 0f;
            wallPullX = 0f;
            const float probe = 10f;
            var bestWallDistance = probe;
            var wallSign = 0f;
            var stepBoost = 0f;

            for (var index = 0; index < solids.Count; index += 1)
            {
                var solid = solids[index];

                // Rising wall face near the deposit (floor-to-wall corner).
                var wallRisesAbove = solid.Top < worldY - BloodCellSize && solid.Bottom > worldY + BloodCellSize;
                if (wallRisesAbove)
                {
                    var distLeft = MathF.Abs(solid.Right - worldX);
                    if (distLeft < bestWallDistance && worldX >= solid.Right - 1f)
                    {
                        bestWallDistance = distLeft;
                        wallSign = -1f;
                    }

                    var distRight = MathF.Abs(solid.Left - worldX);
                    if (distRight < bestWallDistance && worldX <= solid.Left + 1f)
                    {
                        bestWallDistance = distRight;
                        wallSign = 1f;
                    }
                }

                // Step / ledge edge: nearby solid top at a different height.
                if (worldX < solid.Left - probe || worldX > solid.Right + probe)
                {
                    continue;
                }

                var heightDelta = MathF.Abs(solid.Top - (worldY + BloodCellSize * 0.5f));
                if (heightDelta < 2f || heightDelta > 14f)
                {
                    continue;
                }

                var edgeX = worldX < solid.Left
                    ? solid.Left
                    : worldX > solid.Right
                        ? solid.Right
                        : worldX;
                var edgeDist = MathF.Abs(edgeX - worldX);
                if (edgeDist > probe)
                {
                    continue;
                }

                stepBoost = MathF.Max(stepBoost, 1f - (edgeDist / probe));
                if (MathF.Abs(wallSign) < 0.01f && edgeDist > 0.01f)
                {
                    wallSign = MathF.Sign(edgeX - worldX);
                }
            }

            var wallBoost = bestWallDistance < probe
                ? 1f - (bestWallDistance / probe)
                : 0f;
            cornerBoost = Math.Clamp(MathF.Max(wallBoost, stepBoost * 0.85f), 0f, 1f);
            wallPullX = wallSign;
        }

        private void BlendTowardNearbyPools(int centerGx, int centerGy, float amount, bool experimentalCryoTinted)
        {
            if (amount <= 0.05f)
            {
                return;
            }

            for (var oy = -2; oy <= 2; oy += 1)
            {
                for (var ox = -3; ox <= 3; ox += 1)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    var key = (centerGx + ox, centerGy + oy);
                    if (!_game._settledBloodCells.TryGetValue(key, out var neighbour) || neighbour.Amount < 0.2f)
                    {
                        continue;
                    }

                    var distance = MathF.Max(1f, MathF.Sqrt((ox * ox) + (oy * oy)));
                    var bridge = amount * (0.55f / distance);
                    if (bridge < 0.08f)
                    {
                        continue;
                    }

                    // Midpoint fill toward the neighbour so pools meet smoothly.
                    var midGx = centerGx + (ox / 2);
                    var midGy = centerGy + (oy / 2);
                    AddSettledBloodCellAmount(midGx, midGy, bridge, experimentalCryoTinted);
                    if (Math.Abs(ox) > 1)
                    {
                        AddSettledBloodCellAmount(centerGx + Math.Sign(ox), centerGy, bridge * 0.65f, experimentalCryoTinted);
                    }

                    if (Math.Abs(oy) > 1)
                    {
                        AddSettledBloodCellAmount(centerGx, centerGy + Math.Sign(oy), bridge * 0.65f, experimentalCryoTinted);
                    }
                }
            }
        }

        private void AddSettledBloodCellAmount(int gx, int gy, float amount, bool experimentalCryoTinted)
        {
            if (amount <= 0.01f)
            {
                return;
            }

            var key = (gx, gy);
            if (_game._settledBloodCells.TryGetValue(key, out var cell))
            {
                cell.Amount = MathF.Min(SettledBloodMaxAmount, cell.Amount + amount);
                // Fresh blood keeps the pool coherent: rejuvenate so fade stays edge-driven.
                cell.Age = 0;
                cell.ExperimentalCryoTinted |= experimentalCryoTinted;
                return;
            }

            if (_game._settledBloodCells.Count >= MaxSettledBloodCells)
            {
                return;
            }

            _game._settledBloodCells[key] = new SettledBloodCell
            {
                Amount = MathF.Min(SettledBloodMaxAmount, amount),
                DripProgress = 0f,
                Age = 0,
                ExperimentalCryoTinted = experimentalCryoTinted,
            };
        }

        private void TrimSettledBloodCellsIfNeeded()
        {
            var overflow = _game._settledBloodCells.Count - MaxSettledBloodCells;
            if (overflow <= 0)
            {
                return;
            }

            _game._staleSettledBloodCellKeys.Clear();
            foreach (var entry in _game._settledBloodCells)
            {
                if (entry.Value.Amount < 0.4f || entry.Value.Age > SettledBloodFadeStartTicks)
                {
                    _game._staleSettledBloodCellKeys.Add(entry.Key);
                    if (_game._staleSettledBloodCellKeys.Count >= overflow)
                    {
                        break;
                    }
                }
            }

            for (var index = 0; index < _game._staleSettledBloodCellKeys.Count; index += 1)
            {
                _game._settledBloodCells.Remove(_game._staleSettledBloodCellKeys[index]);
            }

            _game._staleSettledBloodCellKeys.Clear();
        }

        private void DrawBloodSquibEffects(Vector2 cameraPosition)
        {
            var normalCells = _game._bloodDrawCellsScratch;
            var cryoCells = _game._bloodCryoDrawCellsScratch;

            // Flight squibs — slightly brighter than ground pools.
            normalCells.Clear();
            cryoCells.Clear();
            for (var index = 0; index < _game._bloodSquibParticles.Count; index += 1)
            {
                var squib = _game._bloodSquibParticles[index];
                AccumulateBloodSquibParticle(
                    squib.ExperimentalCryoTinted ? cryoCells : normalCells,
                    squib.Seed,
                    squib.X,
                    squib.Y,
                    squib.Scale,
                    squib.VelocityX,
                    squib.VelocityY);
            }

            DrawProceduralBloodCells(normalCells, cameraPosition, useCryoColors: false, useFlightColors: true);
            DrawProceduralBloodCells(cryoCells, cameraPosition, useCryoColors: true, useFlightColors: true);

            // Settled pools — previous ground palette; merge overlapping blobs into one silhouette.
            normalCells.Clear();
            cryoCells.Clear();
            foreach (var entry in _game._settledBloodCells)
            {
                if (entry.Value.Amount < 0.08f)
                {
                    continue;
                }

                var cells = entry.Value.ExperimentalCryoTinted ? cryoCells : normalCells;
                AddCellAmount(cells, entry.Key.X, entry.Key.Y, entry.Value.Amount);
            }

            SmoothSettledBloodPools(normalCells);
            SmoothSettledBloodPools(cryoCells);

            DrawProceduralBloodCells(normalCells, cameraPosition, useCryoColors: false, useFlightColors: false);
            DrawProceduralBloodCells(cryoCells, cameraPosition, useCryoColors: true, useFlightColors: false);
        }

        private void SmoothSettledBloodPools(Dictionary<(int, int), float> cells)
        {
            if (cells.Count == 0)
            {
                return;
            }

            var bridgeScratch = _game._bloodBridgeScratch;
            bridgeScratch.Clear();

            // Aggressive morphological close: unify nearby pools into one sheet.
            foreach (var ((gx, gy), amount) in cells)
            {
                if (amount < 0.2f)
                {
                    continue;
                }

                for (var offsetY = -1; offsetY <= 2; offsetY += 1)
                {
                    for (var offsetX = -3; offsetX <= 3; offsetX += 1)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        var key = (gx + offsetX, gy + offsetY);
                        if (cells.ContainsKey(key))
                        {
                            continue;
                        }

                        var neighbourSupport = 0f;
                        var neighbourCount = 0;
                        for (var ny = -1; ny <= 1; ny += 1)
                        {
                            for (var nx = -1; nx <= 1; nx += 1)
                            {
                                if (nx == 0 && ny == 0)
                                {
                                    continue;
                                }

                                if (!cells.TryGetValue((key.Item1 + nx, key.Item2 + ny), out var neighbourAmount)
                                    || neighbourAmount < 0.2f)
                                {
                                    continue;
                                }

                                neighbourSupport += neighbourAmount;
                                neighbourCount += 1;
                            }
                        }

                        if (neighbourCount < 2)
                        {
                            continue;
                        }

                        var fillAmount = MathF.Min(SettledBloodMaxAmount, neighbourSupport / Math.Max(2, neighbourCount));
                        if (offsetY > 0)
                        {
                            fillAmount *= 0.65f;
                        }

                        if (offsetY == 0)
                        {
                            fillAmount = MathF.Max(fillAmount, 0.85f);
                        }

                        if (fillAmount < 0.2f)
                        {
                            continue;
                        }

                        if (!bridgeScratch.TryGetValue(key, out var existing) || fillAmount > existing)
                        {
                            bridgeScratch[key] = fillAmount;
                        }
                    }
                }
            }

            foreach (var entry in bridgeScratch)
            {
                AddCellAmount(cells, entry.Key.Item1, entry.Key.Item2, entry.Value);
            }

            bridgeScratch.Clear();

            // Soften stair/slope steps by filling diagonal corner flats.
            _game._staleSettledBloodCellKeys.Clear();
            foreach (var key in cells.Keys)
            {
                _game._staleSettledBloodCellKeys.Add(key);
            }

            for (var index = 0; index < _game._staleSettledBloodCellKeys.Count; index += 1)
            {
                var (gx, gy) = _game._staleSettledBloodCellKeys[index];
                if (!cells.TryGetValue((gx, gy), out var amount) || amount < 0.25f)
                {
                    continue;
                }

                if (!cells.ContainsKey((gx - 1, gy))
                    && cells.TryGetValue((gx - 1, gy + 1), out var leftDown) && leftDown >= 0.25f)
                {
                    var fill = MathF.Min(SettledBloodMaxAmount, (amount + leftDown) * 0.45f);
                    if (!bridgeScratch.TryGetValue((gx - 1, gy), out var existing) || fill > existing)
                    {
                        bridgeScratch[(gx - 1, gy)] = fill;
                    }
                }

                if (!cells.ContainsKey((gx + 1, gy))
                    && cells.TryGetValue((gx + 1, gy + 1), out var rightDown) && rightDown >= 0.25f)
                {
                    var fill = MathF.Min(SettledBloodMaxAmount, (amount + rightDown) * 0.45f);
                    if (!bridgeScratch.TryGetValue((gx + 1, gy), out var existing) || fill > existing)
                    {
                        bridgeScratch[(gx + 1, gy)] = fill;
                    }
                }
            }

            foreach (var entry in bridgeScratch)
            {
                AddCellAmount(cells, entry.Key.Item1, entry.Key.Item2, entry.Value);
            }

            bridgeScratch.Clear();

            // Weld 2–3 cell horizontal gaps between separate pool blobs.
            _game._staleSettledBloodCellKeys.Clear();
            foreach (var key in cells.Keys)
            {
                _game._staleSettledBloodCellKeys.Add(key);
            }

            for (var index = 0; index < _game._staleSettledBloodCellKeys.Count; index += 1)
            {
                var (gx, gy) = _game._staleSettledBloodCellKeys[index];
                if (!cells.TryGetValue((gx, gy), out var amount) || amount < 0.25f)
                {
                    continue;
                }

                TryWeldHorizontalGap(cells, bridgeScratch, gx, gy, 2);
                TryWeldHorizontalGap(cells, bridgeScratch, gx, gy, 3);
            }

            foreach (var entry in bridgeScratch)
            {
                AddCellAmount(cells, entry.Key.Item1, entry.Key.Item2, entry.Value);
            }

            bridgeScratch.Clear();

            // Downward thickness only on fully interior cells; rim stays single-cell thin.
            _game._staleSettledBloodCellKeys.Clear();
            foreach (var key in cells.Keys)
            {
                _game._staleSettledBloodCellKeys.Add(key);
            }

            for (var index = 0; index < _game._staleSettledBloodCellKeys.Count; index += 1)
            {
                var (gx, gy) = _game._staleSettledBloodCellKeys[index];
                if (!cells.TryGetValue((gx, gy), out var amount) || amount < 0.35f)
                {
                    continue;
                }

                var missingNeighbours = CountMissingBloodNeighbours(cells, gx, gy);
                // Any silhouette rim stays thin — depth only inside the core.
                if (missingNeighbours >= 1)
                {
                    continue;
                }

                var noiseSeed = gx * 374761393 ^ gy * 668265263;
                var dripNoise = GetBloodEdgeNoiseSample(gx, gy + 1, noiseSeed);
                if (amount >= 0.7f && dripNoise > 0.35f)
                {
                    AddCellAmount(cells, gx, gy + 1, amount * (0.2f + (dripNoise * 0.3f)));
                }

                if (amount >= 1.5f && dripNoise > 0.55f)
                {
                    var dripNoise2 = GetBloodEdgeNoiseSample(gx, gy + 2, gx * 9182741 ^ gy * 5013349);
                    if (dripNoise2 > 0.5f)
                    {
                        AddCellAmount(cells, gx, gy + 2, amount * (0.1f + (dripNoise2 * 0.2f)));
                    }
                }
            }

            _game._staleSettledBloodCellKeys.Clear();
        }

        private static int CountMissingBloodNeighbours(Dictionary<(int, int), float> cells, int gx, int gy)
        {
            var missing = 0;
            if (!cells.TryGetValue((gx - 1, gy), out var left) || left < 0.2f)
            {
                missing += 1;
            }

            if (!cells.TryGetValue((gx + 1, gy), out var right) || right < 0.2f)
            {
                missing += 1;
            }

            if (!cells.TryGetValue((gx, gy - 1), out var up) || up < 0.2f)
            {
                missing += 1;
            }

            if (!cells.TryGetValue((gx, gy + 1), out var down) || down < 0.2f)
            {
                missing += 1;
            }

            return missing;
        }

        private static void TryWeldHorizontalGap(
            Dictionary<(int, int), float> cells,
            Dictionary<(int, int), float> bridgeScratch,
            int gx,
            int gy,
            int gapWidth)
        {
            if (gapWidth <= 1)
            {
                return;
            }

            for (var sign = -1; sign <= 1; sign += 2)
            {
                var farKey = (gx + (sign * gapWidth), gy);
                if (!cells.TryGetValue(farKey, out var farAmount) || farAmount < 0.25f)
                {
                    continue;
                }

                if (!cells.TryGetValue((gx, gy), out var nearAmount) || nearAmount < 0.25f)
                {
                    continue;
                }

                var fillAmount = MathF.Min(SettledBloodMaxAmount, (nearAmount + farAmount) * 0.5f);
                for (var step = 1; step < gapWidth; step += 1)
                {
                    var key = (gx + (sign * step), gy);
                    if (cells.ContainsKey(key))
                    {
                        continue;
                    }

                    if (!bridgeScratch.TryGetValue(key, out var existing) || fillAmount > existing)
                    {
                        bridgeScratch[key] = fillAmount;
                    }
                }
            }
        }

        private static void StampCheapBloodDropTrail(
            Dictionary<(int, int), float> cells,
            float x,
            float y,
            float velocityX,
            float velocityY,
            float amount)
        {
            if (amount <= 0.05f)
            {
                return;
            }

            var gx = (int)MathF.Floor(x / BloodCellSize);
            var gy = (int)MathF.Floor(y / BloodCellSize);
            AddCellAmount(cells, gx, gy, amount);

            var speedSquared = (velocityX * velocityX) + (velocityY * velocityY);
            if (speedSquared <= 0.25f)
            {
                return;
            }

            var invLength = 1f / MathF.Sqrt(speedSquared);
            var backX = -(int)MathF.Round(velocityX * invLength);
            var backY = -(int)MathF.Round(velocityY * invLength);
            if (backX == 0 && backY == 0)
            {
                return;
            }

            AddCellAmount(cells, gx + backX, gy + backY, amount * 0.5f);
            AddCellAmount(cells, gx + (backX * 2), gy + (backY * 2), amount * 0.25f);
        }

        private static void AddCellAmount(Dictionary<(int, int), float> cells, int gx, int gy, float amount)
        {
            var key = (gx, gy);
            if (cells.TryGetValue(key, out var existing))
            {
                cells[key] = MathF.Min(SettledBloodMaxAmount, existing + amount);
            }
            else
            {
                cells[key] = amount;
            }
        }

        private static void AccumulateBloodSquibParticle(
            Dictionary<(int, int), float> cells,
            int seed,
            float centerX,
            float centerY,
            float scale,
            float motionX,
            float motionY)
        {
            var motionLengthSquared = (motionX * motionX) + (motionY * motionY);
            var speed = MathF.Sqrt(motionLengthSquared);
            var trajectoryDirection = speed > 0.0001f
                ? new Vector2(motionX / speed, motionY / speed)
                : new Vector2(0f, 1f);

            // Long thin sausage even when nearly stopped (avoid round dots on fall).
            var stretch = Math.Clamp(3.2f + (speed * 0.85f), 3.2f, 6.5f);
            var headRadius = 0.85f * scale;
            var bodyRadius = 0.65f * scale;
            var midRadius = 0.45f * scale;
            var tipRadius = 0.28f * scale;
            var bodyOffset = (0.9f + (stretch * 0.35f)) * MathF.Max(0.55f, scale);
            var midOffset = (1.7f + (stretch * 0.55f)) * MathF.Max(0.55f, scale);
            var tipOffset = (2.6f + (stretch * 0.8f)) * MathF.Max(0.55f, scale);
            var wobble = ((seed & 255) / 255f - 0.5f) * 0.35f * scale;
            var perpendicular = new Vector2(-trajectoryDirection.Y, trajectoryDirection.X);

            var headX = centerX;
            var headY = centerY;
            var bodyX = centerX - (trajectoryDirection.X * bodyOffset) + (perpendicular.X * wobble * 0.35f);
            var bodyY = centerY - (trajectoryDirection.Y * bodyOffset) + (perpendicular.Y * wobble * 0.35f);
            var midX = centerX - (trajectoryDirection.X * midOffset) - (perpendicular.X * wobble * 0.2f);
            var midY = centerY - (trajectoryDirection.Y * midOffset) - (perpendicular.Y * wobble * 0.2f);
            var tipX = centerX - (trajectoryDirection.X * tipOffset) - (perpendicular.X * wobble * 0.45f);
            var tipY = centerY - (trajectoryDirection.Y * tipOffset) - (perpendicular.Y * wobble * 0.45f);

            var noiseRadius = 0.55f * MathF.Max(0.45f, scale);
            var minX = MathF.Min(MathF.Min(headX, bodyX), MathF.Min(midX, tipX));
            var maxX = MathF.Max(MathF.Max(headX, bodyX), MathF.Max(midX, tipX));
            var minY = MathF.Min(MathF.Min(headY, bodyY), MathF.Min(midY, tipY));
            var maxY = MathF.Max(MathF.Max(headY, bodyY), MathF.Max(midY, tipY));
            var pad = MathF.Max(headRadius, tipRadius) + noiseRadius + 1.5f;
            var minGX = (int)MathF.Floor((minX - pad) / BloodCellSize);
            var maxGX = (int)MathF.Floor((maxX + pad) / BloodCellSize);
            var minGY = (int)MathF.Floor((minY - pad) / BloodCellSize);
            var maxGY = (int)MathF.Floor((maxY + pad) / BloodCellSize);

            if (maxGX - minGX > 12)
            {
                var mid = (minGX + maxGX) / 2;
                minGX = mid - 6;
                maxGX = mid + 6;
            }

            if (maxGY - minGY > 12)
            {
                var mid = (minGY + maxGY) / 2;
                minGY = mid - 6;
                maxGY = mid + 6;
            }

            var noiseSeed = seed * 1234567 ^ unchecked((int)0xB100D_A11u);
            var noiseRadiusTimes2 = noiseRadius * 2f;

            for (var gy = minGY; gy <= maxGY; gy += 1)
            {
                var cellCY = (gy * BloodCellSize) + (BloodCellSize * 0.5f);
                for (var gx = minGX; gx <= maxGX; gx += 1)
                {
                    var cellCX = (gx * BloodCellSize) + (BloodCellSize * 0.5f);
                    var sdf = MathF.Min(
                        StretchedCircleSdf(cellCX, cellCY, headX, headY, headRadius, trajectoryDirection, stretch),
                        MathF.Min(
                            StretchedCircleSdf(cellCX, cellCY, bodyX, bodyY, bodyRadius, trajectoryDirection, stretch * 1.2f),
                            MathF.Min(
                                StretchedCircleSdf(cellCX, cellCY, midX, midY, midRadius, trajectoryDirection, stretch * 1.45f),
                                StretchedCircleSdf(cellCX, cellCY, tipX, tipY, tipRadius, trajectoryDirection, stretch * 1.7f))));
                    if (sdf > noiseRadius)
                    {
                        continue;
                    }

                    var rawAlpha = 0.58f - (sdf / noiseRadiusTimes2);
                    var noise = GetBloodEdgeNoiseSample(gx, gy, noiseSeed);
                    var clampedRaw = Math.Clamp(rawAlpha, 0f, 1f);
                    var noisedAlpha = rawAlpha + ((noise - 0.5f) * (1f - clampedRaw) * 0.35f);
                    if (noisedAlpha < 0.4f)
                    {
                        continue;
                    }

                    AddCellAmount(cells, gx, gy, Math.Clamp(rawAlpha, 0.35f, 1f));
                }
            }

            // Larger droplets leave a short motion trail behind the head.
            if (scale >= SquibTrailScaleThreshold && speed > 0.35f)
            {
                var trailSteps = 3 + (int)MathF.Round((scale - SquibTrailScaleThreshold) * 8f);
                var stepX = -trajectoryDirection.X * BloodCellSize;
                var stepY = -trajectoryDirection.Y * BloodCellSize;
                var trailAmount = 0.45f * scale;
                for (var step = 1; step <= trailSteps; step += 1)
                {
                    var tx = centerX + (stepX * step);
                    var ty = centerY + (stepY * step);
                    var gx = (int)MathF.Floor(tx / BloodCellSize);
                    var gy = (int)MathF.Floor(ty / BloodCellSize);
                    AddCellAmount(cells, gx, gy, trailAmount / step);
                    if (scale >= 0.48f)
                    {
                        AddCellAmount(
                            cells,
                            gx + (int)MathF.Round(perpendicular.X),
                            gy + (int)MathF.Round(perpendicular.Y),
                            (trailAmount * 0.35f) / step);
                    }
                }
            }
        }

        private static float StretchedCircleSdf(
            float px,
            float py,
            float circleCx,
            float circleCy,
            float radius,
            Vector2 trajectory,
            float stretchAmount)
        {
            var dx = px - circleCx;
            var dy = py - circleCy;
            var along = (dx * trajectory.X) + (dy * trajectory.Y);
            var perpX = dx - (along * trajectory.X);
            var perpY = dy - (along * trajectory.Y);
            var scaledAlong = along / stretchAmount;
            return MathF.Sqrt((scaledAlong * scaledAlong) + (perpX * perpX) + (perpY * perpY)) - radius;
        }

        private void DrawProceduralBloodCells(
            Dictionary<(int, int), float> cells,
            Vector2 cameraPosition,
            bool useCryoColors,
            bool useFlightColors)
        {
            if (cells.Count == 0)
            {
                return;
            }

            var cellSize = (int)BloodCellSize;
            foreach (var ((gx, gy), _) in cells)
            {
                // Inner silhouette: darker rim cells only — never expand outside the pool.
                var isOutline = !cells.ContainsKey((gx - 1, gy))
                    || !cells.ContainsKey((gx + 1, gy))
                    || !cells.ContainsKey((gx, gy - 1))
                    || !cells.ContainsKey((gx, gy + 1));

                Color pixelColor;
                if (useCryoColors)
                {
                    pixelColor = isOutline
                        ? new Color(140, 195, 220)
                        : new Color(185, 230, 245);
                }
                else if (useFlightColors)
                {
                    pixelColor = isOutline
                        ? new Color(165, 10, 16)
                        : new Color(218, 22, 28);
                }
                else
                {
                    pixelColor = isOutline
                        ? new Color(145, 8, 14)
                        : new Color(198, 16, 24);
                }

                var rect = new Rectangle(
                    (int)MathF.Round((gx * cellSize) - cameraPosition.X),
                    (int)MathF.Round((gy * cellSize) - cameraPosition.Y),
                    cellSize,
                    cellSize);
                _game._spriteBatch.Draw(_game._pixel, rect, pixelColor);
            }
        }

        private static float GetBloodEdgeNoiseSample(int gx, int gy, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)(gx * 374761393);
                hash ^= (uint)(gy * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 1023u) / 1023f;
            }
        }

        private static bool TryFindSolidContainingPoint(
            IReadOnlyList<LevelSolid> solids,
            float worldX,
            float worldY,
            out LevelSolid solid)
        {
            for (var index = 0; index < solids.Count; index += 1)
            {
                var candidate = solids[index];
                if (worldX >= candidate.Left
                    && worldX < candidate.Right
                    && worldY >= candidate.Top
                    && worldY < candidate.Bottom)
                {
                    solid = candidate;
                    return true;
                }
            }

            solid = default;
            return false;
        }

        private static bool TryFindSolidBelowCell(
            IReadOnlyList<LevelSolid> solids,
            float worldX,
            float worldY,
            float cellSize,
            out LevelSolid solid)
        {
            var probeY = worldY + cellSize;
            for (var index = 0; index < solids.Count; index += 1)
            {
                var candidate = solids[index];
                if (worldX < candidate.Left || worldX >= candidate.Right)
                {
                    continue;
                }

                if (worldY <= candidate.Top + 0.01f
                    && probeY >= candidate.Top
                    && worldY >= candidate.Top - cellSize)
                {
                    solid = candidate;
                    return true;
                }
            }

            solid = default;
            return false;
        }
    }
}
