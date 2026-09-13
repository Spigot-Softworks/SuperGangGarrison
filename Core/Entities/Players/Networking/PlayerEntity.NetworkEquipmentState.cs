using System;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private readonly record struct Protocol64ResolvedEquipment(
        GameplayPlayerLoadoutState Loadout,
        GameplayItemDefinition? SecondaryItem);

    public Protocol64EquipmentState CaptureProtocol64EquipmentState()
        => new(
            GameplayLoadoutState.ModPackId,
            GameplayLoadoutState.LoadoutId,
            GameplayLoadoutState.PrimaryItemId,
            GameplayLoadoutState.SecondaryItemId ?? string.Empty,
            GameplayLoadoutState.UtilityItemId ?? string.Empty,
            GameplayLoadoutState.EquippedItemId,
            GameplayLoadoutState.AcquiredItemId ?? string.Empty,
            IsSniperScoped,
            SniperChargeTicks,
            (byte)ExperimentalEngineerAlternateWeaponMode,
            checked((byte)SniperRifleFullyChargedHitStreak));

    public static bool IsValidProtocol64EquipmentState(Protocol64PlayerState? state)
    {
        if (state?.Equipment is null)
        {
            return true;
        }

        return TryResolveProtocol64Equipment(
            state,
            state.GameplayClassId,
            out _);
    }

    private bool ApplyProtocol64EquipmentState(Protocol64PlayerState state)
    {
        if (!TryResolveProtocol64Equipment(state, GameplayClassId, out var resolved))
            return false;

        var equipment = state.Equipment!;
        var loadout = resolved.Loadout;
        var registry = CharacterClassCatalog.RuntimeRegistry;

        // Acquired and ordinary offhand weapons share the Secondary slot.
        // Preserve the transmitted item, not the registry's default preference.
        var validEquippedItem = loadout.EquippedSlot switch
        {
            GameplayEquipmentSlot.Primary => equipment.EquippedItemId == loadout.PrimaryItemId,
            GameplayEquipmentSlot.Secondary => equipment.EquippedItemId == loadout.SecondaryItemId
                || equipment.EquippedItemId == loadout.AcquiredItemId,
            _ => equipment.EquippedItemId == loadout.EquippedItemId,
        };
        if (!validEquippedItem)
            return false;

        SelectedGameplayLoadoutId = loadout.LoadoutId;
        SelectedGameplayPrimaryItemId = loadout.PrimaryItemId;
        RefreshSelectedGameplayPrimaryWeapon();
        ApplyReplicatedAcquiredWeaponState(equipment.AcquiredItemId);

        // The item identity, rather than the current ammo maximum, determines
        // whether an offhand definition exists. This also preserves legitimate
        // zero-ammo/zero-max custom weapons.
        var offhandId = equipment.SecondaryItemId;
        if (string.IsNullOrEmpty(offhandId)
            || resolved.SecondaryItem is null
            || resolved.SecondaryItem.Kind != GameplayItemKind.Weapon)
        {
            if (HasExperimentalOffhandWeapon)
                SetExperimentalOffhandWeapon(null);
        }
        else if (!string.Equals(ResolveRegisteredWeaponItemId(ExperimentalOffhandWeapon), offhandId, StringComparison.Ordinal)
            && resolved.SecondaryItem is not null)
        {
            SetExperimentalOffhandWeapon(registry.CreatePrimaryWeaponDefinition(resolved.SecondaryItem));
        }

        SelectedGameplayEquippedSlot = loadout.EquippedSlot;
        GameplayLoadoutState = loadout with { EquippedItemId = equipment.EquippedItemId };
        ReconcileReplicatedWeaponSelection();
        HydrateEngineerAlternateWeaponMode(equipment.EngineerAlternateWeaponMode);
        IsSniperScoped = state.IsAlive && equipment.IsSniperScoped && HasScopedSniperWeaponEquipped;
        SniperRifleFullyChargedHitStreak = ClassId == PlayerClass.Sniper
            ? Math.Clamp((int)equipment.SniperRifleFullyChargedHitStreak, 0, SniperRifleStreakMaximum)
            : 0;
        SniperChargeTicks = IsSniperScoped
            ? Math.Clamp(equipment.SniperChargeTicks, 0, SniperRifleFullChargeTicks)
            : 0;
        return true;
    }

    private void HydrateEngineerAlternateWeaponMode(int encodedMode)
    {
        var mode = ClassId == PlayerClass.Engineer
            && encodedMode >= 0 && encodedMode <= (int)ExperimentalEngineerAlternateWeaponMode.FreezeRay
            ? (ExperimentalEngineerAlternateWeaponMode)encodedMode
            : ExperimentalEngineerAlternateWeaponMode.None;
        SetExperimentalEngineerAlternateWeaponMode(mode);
        SetExperimentalEngineerEssenceExtractorPresented(
            mode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor
            && IsExperimentalOffhandSelected);
        SetExperimentalEngineerFreezeRayPresented(
            mode == ExperimentalEngineerAlternateWeaponMode.FreezeRay
            && IsExperimentalOffhandSelected);
    }

    /// <summary>
    /// Hydrates the same LTD Engineer mode from the legacy snapshot state
    /// stream. Legacy snapshots carry the generic medigun item, so this
    /// explicit mode keeps weapon, beam, and HUD presentation coherent.
    /// </summary>
    public void HydrateReplicatedEngineerAlternateWeaponMode()
    {
        if (ClassId != PlayerClass.Engineer
            || !TryGetReplicatedStateInt("core.player", "engineer_alternate_weapon_mode", out var encodedMode)
            || encodedMode < 0
            || encodedMode > (int)ExperimentalEngineerAlternateWeaponMode.FreezeRay)
        {
            HydrateEngineerAlternateWeaponMode(0);
            return;
        }

        HydrateEngineerAlternateWeaponMode(encodedMode);
    }

    /// <summary>
    /// Resolves and validates the complete identity carried beside protocol-64
    /// ammo. The registry intentionally repairs invalid loadout overrides, so
    /// every transmitted identity is compared back to the resolved result
    /// before any player state is mutated.
    /// </summary>
    private static bool TryResolveProtocol64Equipment(
        Protocol64PlayerState state,
        string gameplayClassId,
        out Protocol64ResolvedEquipment resolved)
    {
        resolved = default;
        if (state.Equipment is not { } equipment
            || equipment.EngineerAlternateWeaponMode > (byte)ExperimentalEngineerAlternateWeaponMode.FreezeRay
            || !Enum.IsDefined(typeof(GameplayEquipmentSlot), (int)state.ActiveWeapon))
        {
            return false;
        }

        var registry = CharacterClassCatalog.RuntimeRegistry;
        if (!registry.TryGetClassBinding(gameplayClassId, out var binding)
            || !string.Equals(equipment.ModPackId, binding.ModPackId, StringComparison.Ordinal))
        {
            return false;
        }

        var secondaryId = string.IsNullOrEmpty(equipment.SecondaryItemId)
            ? null
            : equipment.SecondaryItemId;
        var acquiredId = string.IsNullOrEmpty(equipment.AcquiredItemId)
            ? null
            : equipment.AcquiredItemId;
        if (!registry.TryCreateValidatedPlayerLoadoutState(
                gameplayClassId,
                equipment.LoadoutId,
                (GameplayEquipmentSlot)state.ActiveWeapon,
                secondaryId,
                acquiredId,
                equipment.PrimaryItemId,
                out var loadout))
        {
            return false;
        }

        // TryCreateValidatedPlayerLoadoutState deliberately falls back to a
        // safe default for invalid overrides. A fallback must be rejected here
        // because applying its ammo would pair the wire counters with another
        // weapon identity.
        if (!string.Equals(loadout.ModPackId, equipment.ModPackId, StringComparison.Ordinal)
            || !string.Equals(loadout.LoadoutId, equipment.LoadoutId, StringComparison.Ordinal)
            || !string.Equals(loadout.PrimaryItemId, equipment.PrimaryItemId, StringComparison.Ordinal)
            || !string.Equals(loadout.SecondaryItemId ?? string.Empty, equipment.SecondaryItemId, StringComparison.Ordinal)
            || !string.Equals(loadout.UtilityItemId ?? string.Empty, equipment.UtilityItemId, StringComparison.Ordinal)
            || !string.Equals(loadout.AcquiredItemId ?? string.Empty, equipment.AcquiredItemId, StringComparison.Ordinal))
        {
            return false;
        }

        var validEquippedItem = loadout.EquippedSlot switch
        {
            GameplayEquipmentSlot.Primary => equipment.EquippedItemId == loadout.PrimaryItemId,
            GameplayEquipmentSlot.Secondary => equipment.EquippedItemId == loadout.SecondaryItemId
                || equipment.EquippedItemId == loadout.AcquiredItemId,
            _ => equipment.EquippedItemId == loadout.EquippedItemId,
        };
        if (!validEquippedItem)
        {
            return false;
        }

        GameplayItemDefinition? secondaryItem = null;
        if (!string.IsNullOrEmpty(equipment.SecondaryItemId))
        {
            if (!registry.TryGetItem(equipment.SecondaryItemId, out secondaryItem)
                || state.OffhandMaxAmmo > 0 && secondaryItem.Kind != GameplayItemKind.Weapon)
            {
                return false;
            }
        }

        resolved = new Protocol64ResolvedEquipment(loadout, secondaryItem);
        return true;
    }
}
