#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int HudElementLayerLocalHealth = 10;
    private const int HudElementLayerLocalWeaponStack = 20;
    private const int HudElementLayerLocalAbilityStack = 21;
    private const int HudElementLayerClassMedic = 30;
    private const int HudElementLayerClassEngineerMetal = 50;
    private const int HudElementLayerClassEngineerSentry = 51;

    private static class HudElementRendererId
    {
        public const string LocalHealth = "local.health.renderer";
        public const string LocalWeaponStack = "local.weapon.stack.renderer";
        public const string LocalAbilityStack = "local.ability.stack.renderer";
        public const string ClassMedicUber = "class.medic.uber.renderer";
        public const string ClassEngineerMetal = "class.engineer.metal.renderer";
        public const string ClassEngineerSentry = "class.engineer.sentry.renderer";
    }

    private HudElementRegistry? _gameplayHudElementRegistry;
    private readonly List<HudElementInstance> _gameplayHudElements = [];

    private HudElementRegistry GameplayHudElementRegistry => _gameplayHudElementRegistry ??= CreateGameplayHudElementRegistry();

    private HudElementRegistry CreateGameplayHudElementRegistry()
    {
        var registry = new HudElementRegistry();
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LocalHealth, HudElementRendererId.LocalHealth, HudElementLayerLocalHealth));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LocalWeaponStack, HudElementRendererId.LocalWeaponStack, HudElementLayerLocalWeaponStack));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LocalAbilityStack, HudElementRendererId.LocalAbilityStack, HudElementLayerLocalAbilityStack));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassMedicUber, HudElementRendererId.ClassMedicUber, HudElementLayerClassMedic));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassEngineerMetal, HudElementRendererId.ClassEngineerMetal, HudElementLayerClassEngineerMetal));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassEngineerSentry, HudElementRendererId.ClassEngineerSentry, HudElementLayerClassEngineerSentry));

        registry.RegisterProvider(new LocalStatusHudProvider());
        registry.RegisterProvider(new WeaponHudProvider());
        registry.RegisterProvider(new AbilityHudProvider());
        registry.RegisterProvider(new ClassAbilityHudProvider());

        registry.RegisterRenderer(HudElementRendererId.LocalHealth, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayLocalStatusHudController.DrawLocalHealthHud()));
        registry.RegisterRenderer(HudElementRendererId.LocalWeaponStack, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayLocalStatusHudController.DrawAmmoHud()));
        registry.RegisterRenderer(HudElementRendererId.LocalAbilityStack, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayLocalStatusHudController.DrawAbilityHud()));
        registry.RegisterRenderer(HudElementRendererId.ClassMedicUber, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayMedicHudController.DrawMedicHud()));
        registry.RegisterRenderer(HudElementRendererId.ClassEngineerMetal, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayEngineerHudController.DrawEngineerMetalHud()));
        registry.RegisterRenderer(HudElementRendererId.ClassEngineerSentry, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayEngineerHudController.DrawEngineerSentryHud()));
        return registry;
    }

    private void CollectGameplayHudElements()
    {
        GameplayHudElementRegistry.Collect(this, _gameplayHudElements);
    }

    private void DrawGameplayHudElements(int minLayerInclusive, int maxLayerInclusive)
    {
        foreach (var element in _gameplayHudElements
                     .Where(element => element.Layer >= minLayerInclusive && element.Layer <= maxLayerInclusive)
                     .OrderBy(static element => element.Layer))
        {
            GameplayHudElementRegistry.Draw(this, element);
        }
    }

    private sealed class LocalStatusHudProvider : IHudElementProvider
    {
        public void Collect(HudElementContext context, List<HudElementInstance> elements)
        {
            if (!context.Game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            context.AddIfRegistered(elements, HudElementId.LocalHealth);
        }
    }

    private sealed class WeaponHudProvider : IHudElementProvider
    {
        public void Collect(HudElementContext context, List<HudElementInstance> elements)
        {
            if (!context.Game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            context.AddIfRegistered(elements, HudElementId.LocalWeaponStack);
        }
    }

    private sealed class AbilityHudProvider : IHudElementProvider
    {
        public void Collect(HudElementContext context, List<HudElementInstance> elements)
        {
            if (!context.Game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            context.AddIfRegistered(elements, HudElementId.LocalAbilityStack);
        }
    }

    private sealed class ClassAbilityHudProvider : IHudElementProvider
    {
        public void Collect(HudElementContext context, List<HudElementInstance> elements)
        {
            var game = context.Game;
            if (!game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            switch (game._world.LocalPlayer.ClassId)
            {
                case PlayerClass.Medic:
                    context.AddIfRegistered(elements, HudElementId.ClassMedicUber);
                    break;
                case PlayerClass.Engineer:
                    context.AddIfRegistered(elements, HudElementId.ClassEngineerMetal);
                    if (game._gameplayEngineerHudController.GetLocalOwnedSentry() is not null)
                    {
                        context.AddIfRegistered(elements, HudElementId.ClassEngineerSentry);
                    }

                    break;
            }
        }
    }
}
