using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LocalPredictionStateOwnershipTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void LocalPredictionStateHasStableManagerOwnershipAndPerGameIsolation()
    {
        var first = CreateGameWithGameplayManager();
        var second = CreateGameWithGameplayManager();

        var firstView = ((IGameplayContext)first.Game).LocalPrediction;
        var secondView = ((IGameplayContext)second.Game).LocalPrediction;

        Assert.Same(first.GameplayManager.LocalPrediction, firstView);
        Assert.Same(firstView, ((IGameplayContext)first.Game).LocalPrediction);
        Assert.Same(firstView, ((IRenderContext)first.Game).LocalPrediction);
        Assert.Same(firstView.PendingPredictedInputs, ((IGameplayContext)first.Game).LocalPrediction.PendingPredictedInputs);
        Assert.Empty(firstView.PendingPredictedInputs);
        Assert.Equal(Vector2.Zero, firstView.PredictedLocalPlayerPosition);
        Assert.Equal(Vector2.Zero, firstView.SmoothedLocalPlayerRenderPosition);
        Assert.Equal(Vector2.Zero, firstView.PredictedLocalPlayerVelocity);
        Assert.False(firstView.HasPredictedLocalPlayerPosition);
        Assert.False(firstView.HasSmoothedLocalPlayerRenderPosition);
        Assert.False(firstView.HasPredictedLocalActionState);
        Assert.Null(firstView.PredictedLocalPlayerShadow);
        Assert.Equal(-1d, firstView.LastPredictedRenderSmoothingTimeSeconds);

        Assert.Same(second.GameplayManager.LocalPrediction, secondView);
        Assert.Same(secondView, ((IGameplayContext)second.Game).LocalPrediction);
        Assert.NotSame(firstView, secondView);
        Assert.NotSame(firstView.PendingPredictedInputs, secondView.PendingPredictedInputs);
    }

    [Fact]
    public void ClearLocalPredictionStateWithoutQueueClearRetainsEntriesAndInputEdges()
    {
        var (game, gameplayManager) = CreateGameWithGameplayManager();
        var state = gameplayManager.LocalPrediction;
        var queue = state.PendingPredictedInputs;
        queue.Add(PredictedInput(1));
        state.PredictedLocalPlayerPosition = new Vector2(10, 20);
        state.SmoothedLocalPlayerRenderPosition = new Vector2(30, 40);
        state.PredictedLocalPlayerRenderCorrectionOffset = new Vector2(5, 6);
        state.PredictedLocalPlayerVelocity = new Vector2(7, 8);
        state.HasPredictedLocalPlayerPosition = true;
        state.HasSmoothedLocalPlayerRenderPosition = true;
        state.HasPredictedLocalActionState = true;
        state.PredictedLocalPlayerGrounded = true;
        state.PredictedSniperRifleChargePendingCount = 2;
        state.PredictedSniperBowChargePendingCount = 3;
        state.LastPredictedRenderSmoothingTimeSeconds = 12.5d;
        SetPrivateField(game, "_pendingPredictedJumpPress", true);

        InvokePrivate(game, "ClearLocalPredictionState", false);

        Assert.Same(queue, state.PendingPredictedInputs);
        Assert.Collection(queue, input => Assert.Equal(1u, input.Sequence));
        Assert.False(state.HasPredictedLocalPlayerPosition);
        Assert.False(state.HasSmoothedLocalPlayerRenderPosition);
        Assert.False(state.HasPredictedLocalActionState);
        Assert.Equal(Vector2.Zero, state.PredictedLocalPlayerRenderCorrectionOffset);
        Assert.Equal(Vector2.Zero, state.PredictedLocalPlayerVelocity);
        Assert.False(state.PredictedLocalPlayerGrounded);
        Assert.Equal(0, state.PredictedSniperRifleChargePendingCount);
        Assert.Equal(0, state.PredictedSniperBowChargePendingCount);
        Assert.Equal(-1d, state.LastPredictedRenderSmoothingTimeSeconds);
        Assert.Equal(new Vector2(10, 20), state.PredictedLocalPlayerPosition);
        Assert.Equal(new Vector2(30, 40), state.SmoothedLocalPlayerRenderPosition);
        Assert.True(GetPrivateField<bool>(game, "_pendingPredictedJumpPress"));
    }

    [Fact]
    public void ClearLocalPredictionStateWithQueueClearClearsEntriesInPlaceAndInputEdges()
    {
        var (game, gameplayManager) = CreateGameWithGameplayManager();
        var state = gameplayManager.LocalPrediction;
        var queue = state.PendingPredictedInputs;
        queue.Add(PredictedInput(1));
        queue.Add(PredictedInput(2));
        state.HasPredictedLocalPlayerPosition = true;
        state.PredictedLocalPlayerRenderCorrectionOffset = new Vector2(5, 6);
        state.PredictedLocalPlayerVelocity = new Vector2(7, 8);
        state.LastPredictedRenderSmoothingTimeSeconds = 12.5d;
        SetPrivateField(game, "_pendingPredictedJumpPress", true);
        SetPrivateField(game, "_pendingImmediateWeaponFirePresentation", true);
        InitializePredictedWeaponFireVisuals(game);

        InvokePrivate(game, "ClearLocalPredictionState", true);

        Assert.Same(queue, state.PendingPredictedInputs);
        Assert.Empty(queue);
        Assert.False(state.HasPredictedLocalPlayerPosition);
        Assert.Equal(Vector2.Zero, state.PredictedLocalPlayerRenderCorrectionOffset);
        Assert.Equal(Vector2.Zero, state.PredictedLocalPlayerVelocity);
        Assert.Equal(-1d, state.LastPredictedRenderSmoothingTimeSeconds);
        Assert.False(GetPrivateField<bool>(game, "_pendingPredictedJumpPress"));
        Assert.False(GetPrivateField<bool>(game, "_pendingImmediateWeaponFirePresentation"));
    }

    [Fact]
    public void RemoveAcknowledgedPredictedInputsPreservesWrappedPrefixAndZeroAckBehavior()
    {
        var (game, gameplayManager) = CreateGameWithGameplayManager();
        var queue = gameplayManager.LocalPrediction.PendingPredictedInputs;
        queue.Add(PredictedInput(uint.MaxValue));
        queue.Add(PredictedInput(1));
        queue.Add(PredictedInput(2));

        InvokePrivate(game, "RemoveAcknowledgedPredictedInputs", 1u);

        Assert.Collection(queue, input => Assert.Equal(2u, input.Sequence));
        InvokePrivate(game, "RemoveAcknowledgedPredictedInputs", 0u);
        Assert.Collection(queue, input => Assert.Equal(2u, input.Sequence));
    }

    private static (Game1 Game, GameplayManager GameplayManager) CreateGameWithGameplayManager()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", Instance)!.SetValue(game, services);
        var gameplayManager = new GameplayManager((IGameplayContext)game);
        services.Register(gameplayManager);
        return (game, gameplayManager);
    }

    private static Game1.PredictedLocalInput PredictedInput(uint sequence)
        => new(sequence, default, false, false, false, false, false, false, false, false, false);

    private static void InitializePredictedWeaponFireVisuals(Game1 game)
    {
        var field = typeof(Game1).GetField("_predictedWeaponFireVisuals", Instance)!;
        field.SetValue(game, Activator.CreateInstance(field.FieldType));
    }

    private static void InvokePrivate(Game1 game, string methodName, params object[] arguments)
    {
        var method = typeof(Game1).GetMethod(methodName, Instance)
            ?? throw new MissingMethodException(typeof(Game1).FullName, methodName);
        method.Invoke(game, arguments);
    }

    private static void SetPrivateField(Game1 game, string fieldName, object value)
    {
        var field = typeof(Game1).GetField(fieldName, Instance)
            ?? throw new MissingFieldException(typeof(Game1).FullName, fieldName);
        field.SetValue(game, value);
    }

    private static T GetPrivateField<T>(Game1 game, string fieldName)
    {
        var field = typeof(Game1).GetField(fieldName, Instance)
            ?? throw new MissingFieldException(typeof(Game1).FullName, fieldName);
        return (T)field.GetValue(game)!;
    }
}
