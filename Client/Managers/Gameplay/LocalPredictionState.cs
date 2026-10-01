#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class LocalPredictionState
{
    internal readonly List<Game1.PredictedLocalInput> PendingPredictedInputs = new();
    internal Vector2 PredictedLocalPlayerPosition;
    internal Vector2 SmoothedLocalPlayerRenderPosition;
    internal Vector2 PredictedLocalPlayerRenderCorrectionOffset;
    internal Vector2 PredictedLocalPlayerVelocity;
    internal bool HasPredictedLocalPlayerPosition;
    internal bool HasSmoothedLocalPlayerRenderPosition;
    internal bool PredictedLocalPlayerGrounded;
    internal PlayerEntity? PredictedLocalPlayerShadow;
    internal Game1.PredictedLocalActionState PredictedLocalActionState;
    internal bool HasPredictedLocalActionState;
    internal bool ServerLocalPredictionEnabled;
    internal PlayerInputSnapshot LatestPredictedLocalInput;
    internal PlayerInputSnapshot PreviousPredictedLocalInput;
    internal ulong LastProtocol64PredictionStateSequence;
    internal int PredictedSniperRifleChargePendingCount;
    internal int PredictedSniperBowChargePendingCount;
    internal double LastPredictedRenderSmoothingTimeSeconds = -1d;
}
