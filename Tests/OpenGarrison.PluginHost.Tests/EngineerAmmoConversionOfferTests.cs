using OpenGarrison.Core.LastToDie;
using OpenGarrison.Client;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class EngineerAmmoConversionOfferTests
{
    [Fact]
    public void OfflineOffersAlsoHideConversionsEvenWhenOtherPerksAreExhausted()
    {
        var runType = typeof(Game1).GetNestedType("LastToDieRunState", BindingFlags.NonPublic)!;
        var survivorType = typeof(Game1).GetNestedType("LastToDieSurvivorKind", BindingFlags.NonPublic)!;
        var perkType = typeof(Game1).GetNestedType("LastToDiePerkKind", BindingFlags.NonPublic)!;
        var conversions = new[] { "EngineerBuckshotConversion", "EngineerPrecisionInstantiator", "EngineerIncendiaryEnhancements" };
        foreach (var chosen in conversions)
        {
            var run = Activator.CreateInstance(runType, ["Truefort"])!;
            runType.GetProperty("SurvivorKind")!.SetValue(run, Enum.Parse(survivorType, "Engineer"));
            var owned = runType.GetProperty("ChosenPerks")!.GetValue(run)!;
            var add = owned.GetType().GetMethod("Add")!;
            foreach (var name in Enum.GetNames(perkType).Where(name => name.StartsWith("Engineer")
                && (!conversions.Contains(name) || name == chosen) && name != "EngineerCaveatInjector"
                && name != "EngineerDestinyPunctuator")) add.Invoke(owned, [Enum.Parse(perkType, name)]);
            var choices = (Array)typeof(Game1).GetMethod("BuildLastToDiePerkChoices", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [run])!;
            var kinds = choices.Cast<object>().Select(choice => choice.GetType().GetProperty("Perk")!.GetValue(choice)!)
                .Select(perk => perk.GetType().GetProperty("Kind")!.GetValue(perk)!.ToString()).ToArray();
            Assert.DoesNotContain(kinds, kind => conversions.Contains(kind));
            Assert.Contains("EngineerCaveatInjector", kinds);
        }
    }

    [Fact]
    public void EveryConversionExcludesTheOthersButAllowsCaveatMissiles()
    {
        var catalog = LastToDieExpansionPerkCatalog.Create(LastToDieSurvivorCatalog.CreateStock());
        var conversions = new[] { LastToDiePerkIds.Engineer.BuckshotConversion,
            LastToDiePerkIds.Engineer.PrecisionInstantiator, LastToDiePerkIds.Engineer.IncendiaryEnhancements };
        foreach (var owned in conversions)
        {
            var survivor = catalog.GetRequired(owned).SurvivorId
                ?? throw new InvalidOperationException($"{owned} must remain survivor-scoped.");
            var eligible = catalog.GetEligible(survivor, new HashSet<LastToDiePerkId> { owned });
            Assert.DoesNotContain(eligible, perk => conversions.Contains(perk.Id));
            Assert.Contains(eligible, perk => perk.Id == LastToDiePerkIds.Engineer.CaveatInjector);
            eligible = catalog.GetEligible(survivor, new HashSet<LastToDiePerkId> { LastToDiePerkIds.Engineer.CaveatInjector });
            Assert.All(conversions, id => Assert.Contains(eligible, perk => perk.Id == id));
        }
    }
}
