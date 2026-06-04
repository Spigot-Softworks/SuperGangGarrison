#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int HudElementLayerLocalHealth = 10;
    private const int HudElementLayerLastToDieBuffIcon = 11;
    private const int HudElementLayerLocalWeaponStack = 20;
    private const int HudElementLayerLocalAbilityStack = 21;
    private const int HudElementLayerLastToDieRage = 9;
    private const int HudElementLayerClassMedic = 30;
    private const int HudElementLayerClassMedicAssist = 31;
    private const int HudElementLayerClassEngineerMetal = 50;
    private const int HudElementLayerClassEngineerSentry = 51;

    private static class HudElementRendererId
    {
        public const string LocalHealth = "local.health.renderer";
        public const string LocalWeaponStack = "local.weapon.stack.renderer";
        public const string LocalAbilityStack = "local.ability.stack.renderer";
        public const string LocalAbilityWidget = "local.ability.widget.renderer";
        public const string LastToDieRage = "last-to-die.rage.renderer";
        public const string LastToDieBuffIcon = "last-to-die.buff-icon.renderer";
        public const string ClassMedicUber = "class.medic.uber.renderer";
        public const string ClassMedicHealingTarget = "class.medic.healing-target.renderer";
        public const string ClassMedicHealer = "class.medic.healer.renderer";
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
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LastToDieRage, HudElementRendererId.LastToDieRage, HudElementLayerLastToDieRage));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LastToDieBuffIcon, HudElementRendererId.LastToDieBuffIcon, HudElementLayerLastToDieBuffIcon));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LocalWeaponStack, HudElementRendererId.LocalWeaponStack, HudElementLayerLocalWeaponStack));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.LocalAbilityStack, HudElementRendererId.LocalAbilityStack, HudElementLayerLocalAbilityStack));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassMedicUber, HudElementRendererId.ClassMedicUber, HudElementLayerClassMedic));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassMedicHealingTarget, HudElementRendererId.ClassMedicHealingTarget, HudElementLayerClassMedicAssist));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassMedicHealer, HudElementRendererId.ClassMedicHealer, HudElementLayerClassMedicAssist + 1));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassEngineerMetal, HudElementRendererId.ClassEngineerMetal, HudElementLayerClassEngineerMetal));
        registry.RegisterDefinition(new HudElementDefinition(HudElementId.ClassEngineerSentry, HudElementRendererId.ClassEngineerSentry, HudElementLayerClassEngineerSentry));

        registry.RegisterProvider(new LocalStatusHudProvider());
        registry.RegisterProvider(new WeaponHudProvider());
        registry.RegisterProvider(new AbilityHudProvider());
        registry.RegisterProvider(new LastToDieHudProvider());
        registry.RegisterProvider(new MedicAssistHudProvider());
        registry.RegisterProvider(new ClassAbilityHudProvider());

        registry.RegisterRenderer(HudElementRendererId.LocalHealth, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayLocalStatusHudController.DrawLocalHealthHud()));
        registry.RegisterRenderer(HudElementRendererId.LocalWeaponStack, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayLocalStatusHudController.DrawAmmoHud()));
        registry.RegisterRenderer(HudElementRendererId.LocalAbilityStack, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayLocalStatusHudController.DrawAbilityHud()));
        registry.RegisterRenderer(HudElementRendererId.LocalAbilityWidget, new DelegateHudElementRenderer(static (context, element) => context.Game._gameplayLocalStatusHudController.DrawAbilityHudElement(element.Id)));
        registry.RegisterRenderer(HudElementRendererId.LastToDieRage, new DelegateHudElementRenderer(static (context, _) => context.Game.DrawLastToDieRageHud()));
        registry.RegisterRenderer(HudElementRendererId.LastToDieBuffIcon, new DelegateHudElementRenderer(static (context, _) => context.Game.DrawLastToDieBuffIcon()));
        registry.RegisterRenderer(HudElementRendererId.ClassMedicUber, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayMedicHudController.DrawMedicHud()));
        registry.RegisterRenderer(HudElementRendererId.ClassMedicHealingTarget, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayMedicHudController.DrawMedicHealingTargetHud()));
        registry.RegisterRenderer(HudElementRendererId.ClassMedicHealer, new DelegateHudElementRenderer(static (context, _) => context.Game._gameplayMedicHudController.DrawMedicHealerHud()));
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

            context.Game._gameplayLocalStatusHudController.CollectAbilityHudElements(elements);
        }
    }

    private sealed class MedicAssistHudProvider : IHudElementProvider
    {
        public void Collect(HudElementContext context, List<HudElementInstance> elements)
        {
            context.Game._gameplayMedicHudController.CollectMedicAssistHudElements(context, elements);
        }
    }

    private sealed class LastToDieHudProvider : IHudElementProvider
    {
        public void Collect(HudElementContext context, List<HudElementInstance> elements)
        {
            var game = context.Game;
            if (!game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            if (game.ShouldDrawLastToDieCombatFeedbackHud())
            {
                context.AddIfRegistered(elements, HudElementId.LastToDieRage);
            }

            if (game.ShouldDrawLastToDieBuffIcon())
            {
                context.AddIfRegistered(elements, HudElementId.LastToDieBuffIcon);
            }
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
                    if (game._hudEditorOpen || game._gameplayEngineerHudController.GetLocalOwnedSentry() is not null)
                    {
                        game._gameplayEngineerHudController.SetEngineerSentryRuntimeDefault();
                        context.AddIfRegistered(elements, HudElementId.ClassEngineerSentry);
                    }

                    break;
            }
        }
    }
}
