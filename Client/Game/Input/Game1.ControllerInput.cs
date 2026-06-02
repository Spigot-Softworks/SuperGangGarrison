#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float ControllerTriggerThreshold = 0.35f;
    private const float ControllerMovementThreshold = 0.25f;
    private const float ControllerSelectionActivityThreshold = 0.18f;
    private const float ControllerAimAssistConeCos = 0.8660254f;
    private const float ControllerAimAssistMaxDistance = 560f;
    private const float ControllerAimAssistMinBlendScale = 0.55f;
    private const float ControllerAimAssistMaxBlendScale = 1.35f;
    private const float ControllerAimAssistMaxBlend = 0.85f;
    private const float ControllerMenuNavigationThreshold = 0.45f;
    private const float ControllerAimInputEngageThreshold = 0.12f;
    private const float ControllerAimInputReleaseThreshold = 0.04f;
    private const float ControllerDirectAimMinTurnRateRadians = 0.15f;
    private const float ControllerDirectAimMaxTurnRateRadians = 8f;
    private const float ControllerLeftStickAimFlickSeconds = 0.08f;
    private const float ControllerLeftStickAimFlickBehindX = 0.5f;
    private const float ControllerOppositeFlickEngageStrength = 0.58f;
    private const float ControllerOppositeFlickHorizontalThreshold = 0.45f;

    private static readonly PlayerIndex[] ControllerPlayerIndices =
    [
        PlayerIndex.One,
        PlayerIndex.Two,
        PlayerIndex.Three,
        PlayerIndex.Four,
    ];

    private static readonly Buttons[] ControllerActivityButtons =
    [
        Buttons.A,
        Buttons.B,
        Buttons.X,
        Buttons.Y,
        Buttons.Back,
        Buttons.Start,
        Buttons.LeftShoulder,
        Buttons.RightShoulder,
        Buttons.LeftStick,
        Buttons.RightStick,
        Buttons.DPadUp,
        Buttons.DPadDown,
        Buttons.DPadLeft,
        Buttons.DPadRight,
    ];

    private static readonly ControllerButtonBinding[] ControllerBindableButtons =
    [
        ControllerButtonBinding.A,
        ControllerButtonBinding.B,
        ControllerButtonBinding.X,
        ControllerButtonBinding.Y,
        ControllerButtonBinding.LeftShoulder,
        ControllerButtonBinding.RightShoulder,
        ControllerButtonBinding.LeftTrigger,
        ControllerButtonBinding.RightTrigger,
        ControllerButtonBinding.Back,
        ControllerButtonBinding.Start,
        ControllerButtonBinding.LeftStick,
        ControllerButtonBinding.RightStick,
        ControllerButtonBinding.DPadUp,
        ControllerButtonBinding.DPadDown,
        ControllerButtonBinding.DPadLeft,
        ControllerButtonBinding.DPadRight,
    ];

    private GamePadState _currentGamePad;
    private GamePadState _previousGamePad;
    private PlayerIndex _currentGamePadPlayerIndex = PlayerIndex.One;
    private bool _hasCurrentGamePadPlayerIndex;
    private bool _controllerPreferred;
    private int _controllerAimDistanceTier;
    private Vector2 _controllerAimDirection = Vector2.UnitX;
    private bool _controllerAimDirectionInitialized;
    private float _controllerScopedAimOffsetY;
    private float _controllerScopedFacingDirectionX = 1f;
    private bool _wasControllerSniperScoped;
    private bool _latestAimUsesController;
    private bool _latestControllerAimLine;
    private bool _hasControllerVisualAimScreenPosition;
    private Vector2 _controllerVisualAimScreenPosition;
    private int _controllerMenuRepeatKey;
    private bool _controllerAimStickEngaged;
    private bool _controllerMenuConfirmConsumed;
    private float _controllerLeftStickAimFlickSecondsRemaining;

    private void UpdateControllerInputState(bool windowActive, KeyboardState keyboard, MouseState mouse)
    {
        _previousGamePad = _currentGamePad;
        _currentGamePad = GetCurrentControllerGamePadState(windowActive, out var selectedControllerChanged);
        if (selectedControllerChanged)
        {
            _previousGamePad = default;
            _controllerAimStickEngaged = false;
            _controllerMenuConfirmConsumed = false;
            ResetControllerMenuRepeat();
        }

        var inputMode = OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(_clientSettings.ControllerInputMode);
        if (!windowActive || inputMode == ControllerInputMode.Off || !_currentGamePad.IsConnected)
        {
            _controllerPreferred = false;
            _controllerAimDirectionInitialized = false;
            _controllerAimStickEngaged = false;
            _controllerLeftStickAimFlickSecondsRemaining = 0f;
            _hasControllerVisualAimScreenPosition = false;
            _latestAimUsesController = false;
            _latestControllerAimLine = false;
            _controllerMenuConfirmConsumed = false;
            return;
        }

        if (!IsControllerMenuConfirmDown())
        {
            _controllerMenuConfirmConsumed = false;
        }

        if (!IsControllerTauntChordDown(_currentGamePad)
            && IsControllerBindingPressed(_clientSettings.ControllerAimDistanceButton, _currentGamePad, _previousGamePad))
        {
            _controllerAimDistanceTier = (_controllerAimDistanceTier + 1) % 3;
        }

        if (inputMode == ControllerInputMode.On)
        {
            _controllerPreferred = true;
            return;
        }

        if (HasGamePadActivity(_currentGamePad))
        {
            _controllerPreferred = true;
        }
        else if (HasKeyboardOrMouseActivity(keyboard, mouse))
        {
            _controllerPreferred = false;
            _controllerAimDirectionInitialized = false;
            _controllerAimStickEngaged = false;
            _controllerLeftStickAimFlickSecondsRemaining = 0f;
            _hasControllerVisualAimScreenPosition = false;
        }
    }

    private GamePadState GetCurrentControllerGamePadState(bool windowActive, out bool selectedControllerChanged)
    {
        selectedControllerChanged = false;
        if (OperatingSystem.IsBrowser() || !windowActive)
        {
            ClearCurrentControllerPlayerIndex(out selectedControllerChanged);
            return default;
        }

        var hasCurrentSlotState = false;
        var currentSlotState = default(GamePadState);
        var hasFallbackState = false;
        var fallbackState = default(GamePadState);
        var fallbackPlayerIndex = PlayerIndex.One;

        foreach (var playerIndex in ControllerPlayerIndices)
        {
            var state = GamePad.GetState(playerIndex);
            if (!state.IsConnected)
            {
                continue;
            }

            if (_hasCurrentGamePadPlayerIndex && playerIndex == _currentGamePadPlayerIndex)
            {
                hasCurrentSlotState = true;
                currentSlotState = state;
            }

            if (HasGamePadSelectionActivity(state))
            {
                SelectCurrentControllerPlayerIndex(playerIndex, out selectedControllerChanged);
                return state;
            }

            if (!hasFallbackState)
            {
                fallbackState = state;
                fallbackPlayerIndex = playerIndex;
                hasFallbackState = true;
            }
        }

        if (hasCurrentSlotState)
        {
            return currentSlotState;
        }

        if (hasFallbackState)
        {
            SelectCurrentControllerPlayerIndex(fallbackPlayerIndex, out selectedControllerChanged);
            return fallbackState;
        }

        ClearCurrentControllerPlayerIndex(out selectedControllerChanged);
        return default;
    }

    private void SelectCurrentControllerPlayerIndex(PlayerIndex playerIndex, out bool selectedControllerChanged)
    {
        selectedControllerChanged = !_hasCurrentGamePadPlayerIndex
            || _currentGamePadPlayerIndex != playerIndex;
        _currentGamePadPlayerIndex = playerIndex;
        _hasCurrentGamePadPlayerIndex = true;
    }

    private void ClearCurrentControllerPlayerIndex(out bool selectedControllerChanged)
    {
        selectedControllerChanged = _hasCurrentGamePadPlayerIndex;
        _hasCurrentGamePadPlayerIndex = false;
        _currentGamePadPlayerIndex = PlayerIndex.One;
    }

    private bool IsControllerGameplayInputActive()
    {
        return _controllerPreferred
            && _currentGamePad.IsConnected
            && OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(_clientSettings.ControllerInputMode) != ControllerInputMode.Off;
    }

    private bool IsControllerButtonPressed(Buttons button)
    {
        return _currentGamePad.IsButtonDown(button)
            && !_previousGamePad.IsButtonDown(button);
    }

    private bool IsControllerButtonDown(Buttons button)
    {
        return IsControllerGameplayInputActive()
            && _currentGamePad.IsButtonDown(button);
    }

    private bool IsControllerTauntChordHeld()
    {
        return IsControllerGameplayInputActive()
            && IsControllerTauntChordDown(_currentGamePad);
    }

    private static bool IsControllerTauntChordDown(GamePadState state)
    {
        return state.IsButtonDown(Buttons.LeftStick)
            && state.IsButtonDown(Buttons.RightStick);
    }

    private bool IsControllerBindingPressed(ControllerButtonBinding binding)
    {
        return IsControllerGameplayInputActive()
            && IsControllerBindingPressed(binding, _currentGamePad, _previousGamePad);
    }

    private bool IsControllerBindingDown(ControllerButtonBinding binding)
    {
        return IsControllerGameplayInputActive()
            && IsControllerBindingDown(binding, _currentGamePad);
    }

    private static bool IsControllerBindingPressed(
        ControllerButtonBinding binding,
        GamePadState current,
        GamePadState previous)
    {
        binding = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(binding);
        return binding switch
        {
            ControllerButtonBinding.LeftTrigger => current.Triggers.Left > ControllerTriggerThreshold
                && previous.Triggers.Left <= ControllerTriggerThreshold,
            ControllerButtonBinding.RightTrigger => current.Triggers.Right > ControllerTriggerThreshold
                && previous.Triggers.Right <= ControllerTriggerThreshold,
            ControllerButtonBinding.None => false,
            _ => TryGetControllerButton(binding, out var button)
                && current.IsButtonDown(button)
                && !previous.IsButtonDown(button),
        };
    }

    private static bool IsControllerBindingDown(ControllerButtonBinding binding, GamePadState state)
    {
        binding = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(binding);
        return binding switch
        {
            ControllerButtonBinding.LeftTrigger => state.Triggers.Left > ControllerTriggerThreshold,
            ControllerButtonBinding.RightTrigger => state.Triggers.Right > ControllerTriggerThreshold,
            ControllerButtonBinding.None => false,
            _ => TryGetControllerButton(binding, out var button) && state.IsButtonDown(button),
        };
    }

    private static bool TryGetControllerButton(ControllerButtonBinding binding, out Buttons button)
    {
        switch (OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(binding))
        {
            case ControllerButtonBinding.A:
                button = Buttons.A;
                return true;
            case ControllerButtonBinding.B:
                button = Buttons.B;
                return true;
            case ControllerButtonBinding.X:
                button = Buttons.X;
                return true;
            case ControllerButtonBinding.Y:
                button = Buttons.Y;
                return true;
            case ControllerButtonBinding.LeftShoulder:
                button = Buttons.LeftShoulder;
                return true;
            case ControllerButtonBinding.RightShoulder:
                button = Buttons.RightShoulder;
                return true;
            case ControllerButtonBinding.Back:
                button = Buttons.Back;
                return true;
            case ControllerButtonBinding.Start:
                button = Buttons.Start;
                return true;
            case ControllerButtonBinding.LeftStick:
                button = Buttons.LeftStick;
                return true;
            case ControllerButtonBinding.RightStick:
                button = Buttons.RightStick;
                return true;
            case ControllerButtonBinding.DPadUp:
                button = Buttons.DPadUp;
                return true;
            case ControllerButtonBinding.DPadDown:
                button = Buttons.DPadDown;
                return true;
            case ControllerButtonBinding.DPadLeft:
                button = Buttons.DPadLeft;
                return true;
            case ControllerButtonBinding.DPadRight:
                button = Buttons.DPadRight;
                return true;
            default:
                button = default;
                return false;
        }
    }

    private bool IsControllerMenuInputActive()
    {
        return _controllerPreferred
            && _currentGamePad.IsConnected
            && OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(_clientSettings.ControllerInputMode) != ControllerInputMode.Off;
    }

    private bool IsControllerMenuConfirmPressed()
    {
        return IsControllerMenuInputActive()
            && !_controllerMenuConfirmConsumed
            && (IsControllerButtonPressed(Buttons.A)
                || IsControllerBindingPressed(_clientSettings.ControllerPrimaryFireButton));
    }

    private void ConsumeControllerMenuConfirmPress()
    {
        if (IsControllerMenuInputActive() && IsControllerMenuConfirmDown())
        {
            _controllerMenuConfirmConsumed = true;
        }
    }

    private bool IsControllerMenuConfirmDown()
    {
        return IsControllerMenuInputActive()
            && (_currentGamePad.IsButtonDown(Buttons.A)
                || IsControllerBindingDown(_clientSettings.ControllerPrimaryFireButton));
    }

    private bool IsControllerMenuBackPressed()
    {
        return IsControllerMenuInputActive()
            && (IsControllerButtonPressed(Buttons.B)
                || IsControllerButtonPressed(Buttons.Start)
                || IsControllerBindingPressed(_clientSettings.ControllerSecondaryFireButton));
    }

    private bool ShouldUseMouseMenuHover(MouseState mouse)
    {
        return !IsControllerMenuInputActive()
            || mouse.X != _previousMouse.X
            || mouse.Y != _previousMouse.Y
            || mouse.ScrollWheelValue != _previousMouse.ScrollWheelValue
            || mouse.LeftButton == ButtonState.Pressed
            || mouse.RightButton == ButtonState.Pressed
            || mouse.MiddleButton == ButtonState.Pressed
            || mouse.XButton1 == ButtonState.Pressed
            || mouse.XButton2 == ButtonState.Pressed;
    }

    private bool TryConsumeControllerMenuNavigation(out int horizontal, out int vertical)
    {
        horizontal = 0;
        vertical = 0;
        if (!IsControllerMenuInputActive())
        {
            ResetControllerMenuRepeat();
            return false;
        }

        var key = GetControllerMenuDirectionKey();
        if (key == 0)
        {
            ResetControllerMenuRepeat();
            return false;
        }

        if (_controllerMenuRepeatKey != 0)
        {
            return false;
        }

        _controllerMenuRepeatKey = key;
        SetControllerMenuNavigationStep(key, out horizontal, out vertical);
        return true;
    }

    private bool TryGetPressedControllerButtonBinding(out ControllerButtonBinding binding)
    {
        binding = ControllerButtonBinding.None;
        if (!IsControllerMenuInputActive())
        {
            return false;
        }

        foreach (var candidate in ControllerBindableButtons)
        {
            if (!IsControllerBindingPressed(candidate, _currentGamePad, _previousGamePad))
            {
                continue;
            }

            binding = candidate;
            return true;
        }

        return false;
    }

    private int GetControllerMenuDirectionKey()
    {
        var stick = _currentGamePad.ThumbSticks.Left;
        var dpad = _currentGamePad.DPad;
        var up = dpad.Up == ButtonState.Pressed || stick.Y > ControllerMenuNavigationThreshold;
        var down = dpad.Down == ButtonState.Pressed || stick.Y < -ControllerMenuNavigationThreshold;
        var left = dpad.Left == ButtonState.Pressed || stick.X < -ControllerMenuNavigationThreshold;
        var right = dpad.Right == ButtonState.Pressed || stick.X > ControllerMenuNavigationThreshold;

        if (up && !down)
        {
            return -2;
        }

        if (down && !up)
        {
            return 2;
        }

        if (left && !right)
        {
            return -1;
        }

        if (right && !left)
        {
            return 1;
        }

        return 0;
    }

    private void ResetControllerMenuRepeat()
    {
        _controllerMenuRepeatKey = 0;
    }

    private static void SetControllerMenuNavigationStep(int key, out int horizontal, out int vertical)
    {
        horizontal = 0;
        vertical = 0;
        if (key == -2)
        {
            vertical = -1;
        }
        else if (key == 2)
        {
            vertical = 1;
        }
        else
        {
            horizontal = Math.Sign(key);
        }
    }

    private static int MoveControllerMenuSelection(int currentIndex, int itemCount, int step)
    {
        if (itemCount <= 0 || step == 0)
        {
            return currentIndex;
        }

        if (currentIndex < 0)
        {
            return step > 0 ? 0 : itemCount - 1;
        }

        return (currentIndex + step + itemCount) % itemCount;
    }

    private static int MoveControllerMenuSelectionClamped(int currentIndex, int itemCount, int step)
    {
        if (itemCount <= 0 || step == 0)
        {
            return currentIndex;
        }

        if (currentIndex < 0)
        {
            return step > 0 ? 0 : itemCount - 1;
        }

        return Math.Clamp(currentIndex + step, 0, itemCount - 1);
    }

    private PlayerInputSnapshot ApplyControllerGameplayInput(PlayerInputSnapshot baseInput, float deltaSeconds)
    {
        if (!IsControllerGameplayInputActive())
        {
            _controllerAimDirectionInitialized = false;
            _controllerAimStickEngaged = false;
            _controllerLeftStickAimFlickSecondsRemaining = 0f;
            _hasControllerVisualAimScreenPosition = false;
            _latestAimUsesController = false;
            _latestControllerAimLine = false;
            return baseInput;
        }

        var aimWorldPosition = UpdateControllerAimWorldPosition(deltaSeconds);
        var leftStick = _currentGamePad.ThumbSticks.Left;
        var dpad = _currentGamePad.DPad;
        var moveDeadzone = MathF.Max(ControllerMovementThreshold, GetControllerAimDeadzone());
        var swapWeaponDown = IsControllerBindingDown(_clientSettings.ControllerSwapWeaponButton);
        var useAbilityDown = IsControllerBindingDown(_clientSettings.ControllerUseAbilityButton)
            || swapWeaponDown;

        return baseInput with
        {
            Left = leftStick.X < -moveDeadzone || dpad.Left == ButtonState.Pressed,
            Right = leftStick.X > moveDeadzone || dpad.Right == ButtonState.Pressed,
            Up = leftStick.Y > moveDeadzone || dpad.Up == ButtonState.Pressed || IsControllerBindingDown(_clientSettings.ControllerJumpButton),
            Down = leftStick.Y < -moveDeadzone || dpad.Down == ButtonState.Pressed,
            FirePrimary = IsControllerBindingDown(_clientSettings.ControllerPrimaryFireButton),
            FireSecondary = IsControllerBindingDown(_clientSettings.ControllerSecondaryFireButton),
            UseAbility = useAbilityDown,
            InteractWeapon = IsControllerBindingDown(_clientSettings.ControllerInteractButton),
            SwapWeapon = swapWeaponDown,
            Taunt = baseInput.Taunt || IsControllerTauntChordHeld(),
            AimWorldX = aimWorldPosition.X,
            AimWorldY = aimWorldPosition.Y,
        };
    }

    private Vector2 UpdateControllerAimWorldPosition(float deltaSeconds)
    {
        var localPlayer = _world.LocalPlayer;
        var rightStick = GetControllerAimStick();
        var stickEngaged = rightStick.LengthSquared() > 0.0001f;
        var isScopedSniper = localPlayer.IsAlive && GetPlayerIsSniperScoped(localPlayer);

        if (!isScopedSniper)
        {
            _wasControllerSniperScoped = false;
            EnsureControllerAimDirectionInitialized(localPlayer);
            if (IsControllerTauntChordHeld())
            {
                _controllerLeftStickAimFlickSecondsRemaining = 0f;
            }
            else if (IsControllerButtonPressed(Buttons.LeftStick))
            {
                StartControllerLeftStickAimFlick();
            }

            if (TryGetControllerLeftStickAimFlickDirection(localPlayer, deltaSeconds, out var flickDirection))
            {
                _latestAimUsesController = true;
                _latestControllerAimLine = ShouldUseControllerAimLine();
                return new Vector2(localPlayer.X, localPlayer.Y) + (flickDirection * GetControllerAimDistance());
            }

            if (stickEngaged)
            {
                _controllerAimDirection = UpdateControllerDirectAimDirection(rightStick, deltaSeconds);
            }

            _latestAimUsesController = true;
            _latestControllerAimLine = ShouldUseControllerAimLine();
            return new Vector2(localPlayer.X, localPlayer.Y) + (_controllerAimDirection * GetControllerAimDistance());
        }

        EnsureControllerAimDirectionInitialized(localPlayer);
        if (!_wasControllerSniperScoped)
        {
            _controllerScopedFacingDirectionX = _controllerAimDirection.X < 0f ? -1f : 1f;
            _controllerScopedAimOffsetY = localPlayer.AimWorldY - localPlayer.Y;
            _wasControllerSniperScoped = true;
        }

        var scopedDistance = GetControllerScopedAimDistance();
        var tierDistance = GetControllerAimDistance();
        var baseDistance = OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1;
        var sensitivityScale = Math.Clamp(baseDistance / Math.Max(1f, tierDistance), 0.25f, 1f);
        _controllerScopedAimOffsetY += rightStick.Y
            * GetControllerScopedPrecisionSpeed()
            * sensitivityScale
            * Math.Max(0f, deltaSeconds);
        _controllerScopedAimOffsetY = Math.Clamp(_controllerScopedAimOffsetY, -scopedDistance, scopedDistance);

        _latestAimUsesController = true;
        _latestControllerAimLine = true;
        return new Vector2(
            localPlayer.X + (_controllerScopedFacingDirectionX * scopedDistance),
            localPlayer.Y + _controllerScopedAimOffsetY);
    }

    private void EnsureControllerAimDirectionInitialized(PlayerEntity localPlayer)
    {
        if (_controllerAimDirectionInitialized && _controllerAimDirection.LengthSquared() > 0.001f)
        {
            return;
        }

        var currentAim = new Vector2(localPlayer.AimWorldX - localPlayer.X, localPlayer.AimWorldY - localPlayer.Y);
        if (currentAim.LengthSquared() <= 0.001f && _hasLatestLocalAimWorldPosition)
        {
            currentAim = new Vector2(_latestLocalAimWorldX - localPlayer.X, _latestLocalAimWorldY - localPlayer.Y);
        }

        if (currentAim.LengthSquared() <= 0.001f)
        {
            currentAim = _controllerAimDirection.LengthSquared() > 0.001f
                ? _controllerAimDirection
                : Vector2.UnitX;
        }

        _controllerAimDirection = Vector2.Normalize(currentAim);
        _controllerAimDirectionInitialized = true;
    }

    private Vector2 UpdateControllerDirectAimDirection(Vector2 rightStick, float deltaSeconds)
    {
        var stickAmount = Math.Clamp(rightStick.Length(), 0f, 1f);
        if (stickAmount <= 0.0001f)
        {
            return _controllerAimDirection;
        }

        var direction = Vector2.Normalize(rightStick);
        if (ShouldControllerOppositeFlickReorient(direction, stickAmount))
        {
            _hasControllerVisualAimScreenPosition = false;
            return direction;
        }

        var targetDirection = TryApplyControllerAimAssist(direction, stickAmount, out var assistedDirection)
                    ? assistedDirection
                    : direction;

        var response = stickAmount * stickAmount;
        var tierSensitivityScale = Math.Clamp(
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1 / Math.Max(1f, GetControllerAimDistance()),
            0.35f,
            1f);
        var turnRate = MathHelper.Lerp(
            ControllerDirectAimMinTurnRateRadians,
            ControllerDirectAimMaxTurnRateRadians,
            response) * tierSensitivityScale;
        var maxTurnRadians = turnRate * Math.Clamp(deltaSeconds, 1f / 240f, 0.1f);

        return RotateControllerAimDirectionToward(_controllerAimDirection, targetDirection, maxTurnRadians);
    }

    private void StartControllerLeftStickAimFlick()
    {
        _controllerLeftStickAimFlickSecondsRemaining = ControllerLeftStickAimFlickSeconds;
        _hasControllerVisualAimScreenPosition = false;
    }

    private bool TryGetControllerLeftStickAimFlickDirection(
        PlayerEntity localPlayer,
        float deltaSeconds,
        out Vector2 flickDirection)
    {
        flickDirection = default;
        if (_controllerLeftStickAimFlickSecondsRemaining <= 0f)
        {
            return false;
        }

        flickDirection = GetControllerDownBehindAimFlickDirection(localPlayer);
        _controllerLeftStickAimFlickSecondsRemaining = MathF.Max(
            0f,
            _controllerLeftStickAimFlickSecondsRemaining - MathF.Max(0f, deltaSeconds));
        if (_controllerLeftStickAimFlickSecondsRemaining <= 0f)
        {
            _hasControllerVisualAimScreenPosition = false;
        }

        return true;
    }

    private Vector2 GetControllerDownBehindAimFlickDirection(PlayerEntity localPlayer)
    {
        var facingX = GetControllerFacingDirectionX(localPlayer);
        return Vector2.Normalize(new Vector2(-facingX * ControllerLeftStickAimFlickBehindX, 1f));
    }

    private bool ShouldControllerOppositeFlickReorient(Vector2 direction, float stickAmount)
    {
        if (!_clientSettings.ControllerFlickToChangeDirections
            || stickAmount < ControllerOppositeFlickEngageStrength
            || !_world.LocalPlayer.IsAlive
            || GetPlayerIsSniperScoped(_world.LocalPlayer))
        {
            return false;
        }

        var facingX = GetControllerFacingDirectionX(_world.LocalPlayer);
        return direction.X * facingX <= -ControllerOppositeFlickHorizontalThreshold;
    }

    private float GetControllerFacingDirectionX(PlayerEntity localPlayer)
    {
        if (_controllerAimDirectionInitialized && MathF.Abs(_controllerAimDirection.X) > 0.05f)
        {
            return _controllerAimDirection.X < 0f ? -1f : 1f;
        }

        var aimDeltaX = localPlayer.AimWorldX - localPlayer.X;
        if (MathF.Abs(aimDeltaX) > 0.05f)
        {
            return aimDeltaX < 0f ? -1f : 1f;
        }

        return _controllerScopedFacingDirectionX < 0f ? -1f : 1f;
    }

    private static Vector2 RotateControllerAimDirectionToward(Vector2 currentDirection, Vector2 targetDirection, float maxTurnRadians)
    {
        if (currentDirection.LengthSquared() <= 0.001f)
        {
            return targetDirection;
        }

        var currentAngle = MathF.Atan2(currentDirection.Y, currentDirection.X);
        var targetAngle = MathF.Atan2(targetDirection.Y, targetDirection.X);
        var deltaAngle = WrapControllerAimAngle(targetAngle - currentAngle);
        if (MathF.Abs(deltaAngle) <= maxTurnRadians)
        {
            return targetDirection;
        }

        var nextAngle = currentAngle + Math.Clamp(deltaAngle, -maxTurnRadians, maxTurnRadians);
        return new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle));
    }

    private static float WrapControllerAimAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathHelper.TwoPi;
        }

        while (angle < -MathF.PI)
        {
            angle += MathHelper.TwoPi;
        }

        return angle;
    }

    private bool TryApplyControllerAimAssist(Vector2 rawDirection, float stickAmount, out Vector2 assistedDirection)
    {
        assistedDirection = rawDirection;
        var strength = OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(_clientSettings.ControllerAimAssistStrength);
        if (!_clientSettings.ControllerAimAssistEnabled
            || strength <= 0f
            || !_world.LocalPlayer.IsAlive
            || GetPlayerIsSniperScoped(_world.LocalPlayer))
        {
            return false;
        }

        var localPlayer = _world.LocalPlayer;
        var localPosition = new Vector2(localPlayer.X, localPlayer.Y);
        var bestScore = float.NegativeInfinity;
        var bestDot = 0f;
        var bestDirection = Vector2.Zero;
        foreach (var candidate in EnumerateRemotePlayersForView())
        {
            if (!candidate.IsAlive
                || candidate.Team == localPlayer.Team
                || GetPlayerVisibilityAlpha(candidate) <= 0f
                || IsSpyHiddenFromLocalViewer(candidate))
            {
                continue;
            }

            var candidatePosition = new Vector2(candidate.X, candidate.Y);
            var toCandidate = candidatePosition - localPosition;
            var distanceSquared = toCandidate.LengthSquared();
            if (distanceSquared <= 0.001f
                || distanceSquared > ControllerAimAssistMaxDistance * ControllerAimAssistMaxDistance)
            {
                continue;
            }

            var distance = MathF.Sqrt(distanceSquared);
            var targetDirection = toCandidate / distance;
            var dot = Vector2.Dot(rawDirection, targetDirection);
            if (dot < ControllerAimAssistConeCos
                || !_world.QueryHasLineOfSight(localPlayer.X, localPlayer.Y, candidate.X, candidate.Y, localPlayer.Team))
            {
                continue;
            }

            var score = dot - (distance / ControllerAimAssistMaxDistance * 0.12f);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestDot = dot;
            bestDirection = targetDirection;
        }

        if (bestDirection == Vector2.Zero)
        {
            return false;
        }

        var coneInfluence = Math.Clamp((bestDot - ControllerAimAssistConeCos) / (1f - ControllerAimAssistConeCos), 0f, 1f);
        var lowInputBonus = MathHelper.Lerp(1.15f, 1f, Math.Clamp(stickAmount, 0f, 1f));
        var blend = Math.Clamp(
            strength * MathHelper.Lerp(ControllerAimAssistMinBlendScale, ControllerAimAssistMaxBlendScale, coneInfluence) * lowInputBonus,
            0f,
            ControllerAimAssistMaxBlend);
        assistedDirection = Vector2.Normalize(Vector2.Lerp(rawDirection, bestDirection, blend));
        return true;
    }

    private Vector2 GetEffectiveAimScreenPosition(MouseState mouse, Vector2 cameraPosition)
    {
        if (_latestAimUsesController && _hasLatestLocalAimWorldPosition)
        {
            var localPlayer = _world.LocalPlayer;
            var aimOffset = new Vector2(_latestLocalAimWorldX - localPlayer.X, _latestLocalAimWorldY - localPlayer.Y);
            var renderAnchor = localPlayer.IsAlive
                ? GetRenderPosition(localPlayer)
                : new Vector2(localPlayer.X, localPlayer.Y);
            var targetScreenPosition = RoundToSourcePixels(renderAnchor + aimOffset - cameraPosition);
            return GetSmoothedControllerVisualAimScreenPosition(targetScreenPosition);
        }

        _hasControllerVisualAimScreenPosition = false;
        return new Vector2(mouse.X, mouse.Y);
    }

    private Vector2 GetSmoothedControllerVisualAimScreenPosition(Vector2 targetScreenPosition)
    {
        if (!_hasControllerVisualAimScreenPosition
            || Vector2.DistanceSquared(_controllerVisualAimScreenPosition, targetScreenPosition) > 96f * 96f)
        {
            _controllerVisualAimScreenPosition = targetScreenPosition;
            _hasControllerVisualAimScreenPosition = true;
            return targetScreenPosition;
        }

        var elapsedSeconds = Math.Clamp(_clientUpdateElapsedSeconds, 1f / 240f, 0.1f);
        var blend = 1f - MathF.Exp(-26f * elapsedSeconds);
        _controllerVisualAimScreenPosition = Vector2.Lerp(_controllerVisualAimScreenPosition, targetScreenPosition, blend);
        return RoundToSourcePixels(_controllerVisualAimScreenPosition);
    }

    private bool ShouldDrawControllerAimLine()
    {
        return _latestAimUsesController && _latestControllerAimLine;
    }

    private Vector2 GetControllerAimStick()
    {
        var stick = _currentGamePad.ThumbSticks.Right;
        var screenDirection = new Vector2(stick.X, -stick.Y);
        var deadzone = GetControllerAimDeadzone();
        var length = screenDirection.Length();
        if (length <= deadzone)
        {
            _controllerAimStickEngaged = false;
            return Vector2.Zero;
        }

        var normalizedLength = Math.Clamp((length - deadzone) / (1f - deadzone), 0f, 1f);
        var threshold = _controllerAimStickEngaged
            ? ControllerAimInputReleaseThreshold
            : ControllerAimInputEngageThreshold;
        if (normalizedLength < threshold)
        {
            _controllerAimStickEngaged = false;
            return Vector2.Zero;
        }

        _controllerAimStickEngaged = true;
        return Vector2.Normalize(screenDirection) * normalizedLength;
    }

    private float GetControllerAimDistance()
    {
        return _controllerAimDistanceTier switch
        {
            1 => OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(
                _clientSettings.ControllerAimDistanceTier2,
                OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2),
            2 => OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(
                _clientSettings.ControllerAimDistanceTier3,
                OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3),
            _ => OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(
                _clientSettings.ControllerAimDistanceTier1,
                OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1),
        };
    }

    private float GetControllerScopedAimDistance()
    {
        return Math.Max(384f, GetControllerAimDistance() * 4f);
    }

    private float GetControllerAimDeadzone()
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(_clientSettings.ControllerAimDeadzone);
    }

    private float GetControllerScopedPrecisionSpeed()
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(_clientSettings.ControllerScopedPrecisionSpeed);
    }

    private bool ShouldUseControllerAimLine()
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(_clientSettings.ControllerReticleMode) == ControllerReticleMode.AimLine;
    }

    private static bool HasGamePadActivity(GamePadState state)
    {
        return state.ThumbSticks.Left.LengthSquared() > 0.01f
            || state.ThumbSticks.Right.LengthSquared() > 0.01f
            || state.Triggers.Left > 0.01f
            || state.Triggers.Right > 0.01f
            || ControllerActivityButtons.Any(state.IsButtonDown);
    }

    private static bool HasGamePadSelectionActivity(GamePadState state)
    {
        var thresholdSquared = ControllerSelectionActivityThreshold * ControllerSelectionActivityThreshold;
        return state.ThumbSticks.Left.LengthSquared() > thresholdSquared
            || state.ThumbSticks.Right.LengthSquared() > thresholdSquared
            || state.Triggers.Left > ControllerSelectionActivityThreshold
            || state.Triggers.Right > ControllerSelectionActivityThreshold
            || ControllerActivityButtons.Any(state.IsButtonDown);
    }

    private bool HasKeyboardOrMouseActivity(KeyboardState keyboard, MouseState mouse)
    {
        if (mouse.X != _previousMouse.X
            || mouse.Y != _previousMouse.Y
            || mouse.ScrollWheelValue != _previousMouse.ScrollWheelValue
            || mouse.LeftButton == ButtonState.Pressed
            || mouse.RightButton == ButtonState.Pressed
            || mouse.MiddleButton == ButtonState.Pressed
            || mouse.XButton1 == ButtonState.Pressed
            || mouse.XButton2 == ButtonState.Pressed)
        {
            return true;
        }

        foreach (var key in keyboard.GetPressedKeys())
        {
            if (!_previousKeyboard.IsKeyDown(key))
            {
                return true;
            }
        }

        return false;
    }
}
