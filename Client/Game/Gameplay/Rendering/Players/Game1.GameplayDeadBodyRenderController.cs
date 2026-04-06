#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayDeadBodyRenderController
    {
        private readonly Game1 _game;

        public GameplayDeadBodyRenderController(Game1 game)
        {
            _game = game;
        }

        public void DrawDeadBody(DeadBodyEntity deadBody, Vector2 cameraPosition)
        {
            var renderPosition = _game.GetRenderPosition(deadBody.Id, deadBody.X, deadBody.Y);
            DrawDeadBodyVisual(deadBody.Id, deadBody.SourcePlayerId, deadBody.ClassId, deadBody.Team, deadBody.AnimationKind, renderPosition.X, renderPosition.Y, deadBody.Width, deadBody.Height, deadBody.FacingLeft, deadBody.TicksRemaining, cameraPosition);
        }

        public void DrawRetainedDeadBodies(Vector2 cameraPosition, int? skippedDeadBodySourcePlayerId = null)
        {
            for (var index = 0; index < _game._retainedDeadBodies.Count; index += 1)
            {
                var deadBody = _game._retainedDeadBodies[index];
                if (skippedDeadBodySourcePlayerId.HasValue && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
                {
                    continue;
                }

                DrawDeadBodyVisual(deadBody.Id, deadBody.SourcePlayerId, deadBody.ClassId, deadBody.Team, deadBody.AnimationKind, deadBody.X, deadBody.Y, deadBody.Width, deadBody.Height, deadBody.FacingLeft, deadBody.TicksRemaining, cameraPosition);
            }
        }

        public void SyncRetainedDeadBodies()
        {
            if (_game._corpseDurationMode != ClientSettings.CorpseDurationInfinite)
            {
                ResetRetainedDeadBodies();
                return;
            }

            _game._staleTrackedDeadBodyIds.Clear();
            foreach (var trackedId in _game._trackedDeadBodyVisuals.Keys)
            {
                _game._staleTrackedDeadBodyIds.Add(trackedId);
            }

            foreach (var deadBody in _game._world.DeadBodies)
            {
                var renderPosition = _game.GetRenderPosition(deadBody.Id, deadBody.X, deadBody.Y);
                _game._trackedDeadBodyVisuals[deadBody.Id] = new RetainedDeadBodyVisual(deadBody.Id, deadBody.SourcePlayerId, deadBody.ClassId, deadBody.Team, deadBody.AnimationKind, renderPosition.X, renderPosition.Y, deadBody.Width, deadBody.Height, deadBody.FacingLeft, deadBody.TicksRemaining);
                _game._staleTrackedDeadBodyIds.Remove(deadBody.Id);
            }

            for (var index = 0; index < _game._staleTrackedDeadBodyIds.Count; index += 1)
            {
                var deadBodyId = _game._staleTrackedDeadBodyIds[index];
                if (_game._trackedDeadBodyVisuals.TryGetValue(deadBodyId, out var retainedDeadBody))
                {
                    _game._retainedDeadBodies.Add(retainedDeadBody);
                    _game._trackedDeadBodyVisuals.Remove(deadBodyId);
                }
            }
        }

        public void ResetRetainedDeadBodies()
        {
            _game._trackedDeadBodyVisuals.Clear();
            _game._retainedDeadBodies.Clear();
            _game._staleTrackedDeadBodyIds.Clear();
        }

        public ClientDeadBodyAnimationKind ResolveClientPluginDeadBodyAnimationKind(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind)
        {
            if (TryGetForcedLastToDieDeadBodyAnimationKind(sourcePlayerId, classId, team, animationKind, out var forcedAnimationKind))
            {
                return forcedAnimationKind;
            }

            return ToClientDeadBodyAnimationKind(animationKind);
        }

        public void DrawDeadBodyVisual(int id, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float x, float y, float width, float height, bool facingLeft, int ticksRemaining, Vector2 cameraPosition)
        {
            DrawDeadBodyVisualCore(id, sourcePlayerId, classId, team, animationKind, x, y, width, height, facingLeft, ticksRemaining, cameraPosition);
        }

        public bool TryGetForcedLastToDieDeadBodyAnimationKind(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind deadBodyAnimationKind, out ClientDeadBodyAnimationKind forcedAnimationKind)
        {
            return TryGetForcedLastToDieDeadBodyAnimationKindCore(sourcePlayerId, classId, team, deadBodyAnimationKind, out forcedAnimationKind);
        }

        private void DrawDeadBodyVisualCore(int id, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float x, float y, float width, float height, bool facingLeft, int ticksRemaining, Vector2 cameraPosition)
        {
            var renderPosition = new Vector2(x, y);
            var pluginAnimationKind = ResolveClientPluginDeadBodyAnimationKind(sourcePlayerId, classId, team, animationKind);
            if (_game.TryDrawClientPluginDeadBody(cameraPosition, new ClientDeadBodyRenderState(id, ToClientPluginClass(classId), ToClientPluginTeam(team), renderPosition, width, height, facingLeft, ticksRemaining, pluginAnimationKind)))
            {
                return;
            }

            var spriteName = Game1.GetDeadBodySpriteName(classId, team, animationKind);
            if (spriteName is not null)
            {
                var sprite = _game._runtimeAssets.GetSprite(spriteName);
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
                    _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[0], new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y), Color.White, 0f, sprite.Origin.ToVector2(), new Vector2(facingLeft ? -1f : 1f, 1f));
                    return;
                }
            }

            var rectangle = new Rectangle((int)(renderPosition.X - (width / 2f) - cameraPosition.X), (int)(renderPosition.Y - (height / 2f) - cameraPosition.Y), (int)width, (int)height);
            _game._spriteBatch.Draw(_game._pixel, rectangle, team == PlayerTeam.Blue ? new Color(24, 45, 80) : new Color(90, 30, 30));
        }

        private bool TryGetForcedLastToDieDeadBodyAnimationKindCore(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind deadBodyAnimationKind, out ClientDeadBodyAnimationKind forcedAnimationKind)
        {
            if (!_game.IsLastToDieSessionActive || _game._networkClient.IsSpectator || sourcePlayerId != _game._world.LocalPlayer.Id || team != PlayerTeam.Red || deadBodyAnimationKind == DeadBodyAnimationKind.Decapitated)
            {
                forcedAnimationKind = default;
                return false;
            }

            forcedAnimationKind = classId switch
            {
                PlayerClass.Soldier => ClientDeadBodyAnimationKind.Rifle,
                PlayerClass.Demoman => ClientDeadBodyAnimationKind.Rifle,
                _ => default,
            };
            return forcedAnimationKind != default;
        }
    }
}
