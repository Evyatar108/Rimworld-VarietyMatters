using VarietyMatters;
using Verse;

AssertEqual(
	"Core-Human",
	RaceKeyUtility.GetRaceKey(new ThingDef
	{
		defName = "Human",
		modContentPack = new ModContentPack { Name = "Core" }
	}));

AssertEqual(
	"null-OwnerlessHumanlikeRace",
	RaceKeyUtility.GetRaceKey(new ThingDef { defName = "OwnerlessHumanlikeRace" }));

AssertEqual(
	"null-NamelessOwnerRace",
	RaceKeyUtility.GetRaceKey(new ThingDef
	{
		defName = "NamelessOwnerRace",
		modContentPack = new ModContentPack()
	}));

static void AssertEqual(string expected, string actual)
{
	if (actual != expected)
	{
		throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
	}
}
