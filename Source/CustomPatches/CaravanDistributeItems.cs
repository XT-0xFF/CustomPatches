using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace CustomPatches
{
	public static class CaravanDistributeItems
	{
		public static void DistributeThingsForPoliciesAndCarry(IEnumerable<Pawn> caravanPawns)
		{
			// get all required thingDefs from drug policies and carry assignments for all colonist pawns in the caravan
			var reqTotalAll = new Dictionary<ThingDef, int>();
			var reqCountPawns = new Dictionary<ThingDef, Dictionary<Pawn, int>>();
			foreach (var pawn in caravanPawns)
			{
				// only check colonists for required stuff
				if (pawn.IsColonist)
				{
					// create list of all thingDefs and their count required for this pawn via their drug policy and carry assignment
					foreach (var req in pawn.GetAllRequired())
					{
						var thingDef = req.Key;
						var count = req.Value;

						// remember the count of thingDef required in total and for this pawn
						if (!reqTotalAll.ContainsKey(thingDef))
						{
							reqTotalAll.Add(thingDef, count);
							reqCountPawns.Add(thingDef, new Dictionary<Pawn, int> { { pawn, count } });
						}
						else
						{
							reqTotalAll[thingDef] += count;
							reqCountPawns[thingDef].Add(pawn, count);
						}
					}
				}
			}

			// get all things and their counts for the required thingDefs in the inventories of all pawns in the caravan
			var invTotalAll = new Dictionary<ThingDef, int>();
			var invCountPawns = new Dictionary<ThingDef, Dictionary<Pawn, int>>();
			var inventoriesAll = new Dictionary<ThingDef, Dictionary<Pawn, List<Thing>>>();
			// create dictionaries for required thingDefs
			foreach (var thingDef in reqTotalAll.Keys)
			{
				invTotalAll.Add(thingDef, 0);
				invCountPawns.Add(thingDef, new Dictionary<Pawn, int>());
				inventoriesAll.Add(thingDef, new Dictionary<Pawn, List<Thing>>());
			}
			// check each pawn's inventory for required things
			foreach (var pawn in caravanPawns)
			{
				foreach (var thing in pawn.inventory.innerContainer)
				{
					var thingDef = thing.def;

					// sort out only things that are required
					if (invTotalAll.ContainsKey(thingDef))
					{
						var count = thing.stackCount;

						// remember the total of required thingDef found in all inventories
						invTotalAll[thingDef] += count;

						// remember the count of required thingDef found in this pawn's inventory
						var invCountForPawn = invCountPawns[thingDef];
						var inventoryForPawn = inventoriesAll[thingDef];
						if (!invCountForPawn.ContainsKey(pawn))
						{
							invCountForPawn.Add(pawn, count);
							inventoryForPawn.Add(pawn, new List<Thing> { thing });
						}
						else
						{
							invCountForPawn[pawn] += count;
							inventoriesAll[thingDef][pawn].Add(thing);
						}
					}
				}
			}

			// iterate over all required thingDefs
			foreach (var thingDef in reqTotalAll.Keys)
			{
				//Log.Message($"--- Thing: {thingDef}");

				// debug output: required
				//Log.Message($"- req total: {reqTotalAll[thingDef]}");
				//foreach (var pair in reqCountPawns[thingDef])
				//	Log.Message($"{pair.Value} -> {pair.Key}");

				// debug output: inventory
				//if (invTotalAll.ContainsKey(thingDef))
				//{
				//	Log.Message($"- inv total: {invTotalAll[thingDef]}");
				//	foreach (var pair in invCountPawns[thingDef])
				//		Log.Message($"{pair.Value} -> {pair.Key}");
				//}

				// check if any pawn has thingDef things in their inventory
				var invTotal = invTotalAll[thingDef];
				if (invTotal == 0)
				{
					Log.Message($"None of {thingDef} found in inventories; continuing");
					continue;
				}

				// get required and inventory count pawn lists for thingDef
				var reqCountPawn = reqCountPawns[thingDef];
				var invCountPawn = invCountPawns[thingDef];

				// if more is required than available in inventories, distribute the available amount in relation to the required amount per pawn
				var reqTotal = reqTotalAll[thingDef];
				if (reqTotal > invTotal)
				{
					var ratio = invTotal / (float)reqTotal;
					var remainder = invTotal;
					foreach (var pawn in reqCountPawn.Keys.ToList())
					{
						var count = (int)Mathf.Round(reqCountPawn[pawn] * ratio);
						reqCountPawn[pawn] = count;
						remainder -= count;
					}
#warning TODO quite sure there's still a bug here, test: req 100, 100, 100, 0 - inv 0, 0, 0, 1 -> should end with remainder 1
					if (remainder != 0)
						Log.Warning($"Remainder ({remainder}) was not 0 after limiting distribution for {thingDef}; {reqTotal} was required, {invTotal} available, ratio is {ratio}");

					//Log.Message($"- limiting (reqTotal > invTotal)");
					//foreach (var pair in reqCountPawns[thingDef])
					//	Log.Message($"{pair.Value} -> {pair.Key}");
				}

				Distribute(thingDef, reqCountPawn, invCountPawn, inventoriesAll[thingDef]);
			}
		}

		private static void Distribute(
			ThingDef thingDef, 
			Dictionary<Pawn, int> reqThingDef,
			Dictionary<Pawn, int> invThingDef,
			Dictionary<Pawn, List<Thing>> inventories)
		{
			// check for required thingDefs already being in the correct inventory
			foreach (var pawn in reqThingDef.Keys.ToList())
			{
				// pawn requires thingDef and has it in inventory
				if (invThingDef.ContainsKey(pawn))
				{
					var inv = invThingDef[pawn];
					var req = reqThingDef[pawn];
					// has more in inventory than required
					if (inv > req)
					{
						// remove pawn from required-list and reduce inventory count for pawn
						reqThingDef.Remove(pawn);
						invThingDef[pawn] -= req;
					}
					// has exactly the amount required in inventory
					else if (inv == req)
					{
						// remove pawn from required- and inventory-list
						reqThingDef.Remove(pawn);
						invThingDef.Remove(pawn);
					}
					// has less in inventory than required
					else
					{
						// reduce required count and remove from inventory-list
						reqThingDef[pawn] -= inv;
						invThingDef.Remove(pawn);
					}
				}
			}

			// sort list so that non-colonists are iterated over first
			invThingDef.OrderBy((p) => p.Key.IsColonist ? 1 : 0);

			//Log.Message($"--- Looking for {thingDef}");

			// get pawns requiring thingDef and pawns with thingDef in their inventory as lists as to not cause problems when removing entries while iterating
			var reqPawns = reqThingDef.Keys.ToList();
			var invPawns = invThingDef.Keys.ToList();

			// iterate over pawns requiring thingDef
			for (int i = 0; i < reqPawns.Count;)
			{
				var reqPawn = reqPawns[i];
				var req = reqThingDef[reqPawn];
				//Log.Message($"req {req} {reqPawn}");

				// iterate over pawns with thingDef in their inventory
				for (int j = 0; j < invPawns.Count;)
				{
					var invPawn = invPawns[j];

					// at this point a pawn can only be in one of the two lists, not in both, as we already sorted the other cases out at the start of the method
					if (reqPawn == invPawn)
					{
						Log.Error($"Error trying to distribute item {thingDef.defName} between pawns; reqPawn ({reqPawn}) == invPawn ({invPawn})!");
						continue;
					}

					var inv = invThingDef[invPawn];
					//Log.Message($"inv {inv} {invPawn}");

					int count;
					// invPawn has more thingDef in inventory than reqPawn requires
					if (inv > req)
					{
						// remove reqPawn from required-list and decrease amount of thingDef in invPawn inventory
						reqPawns.Remove(reqPawn);
						invThingDef[invPawn] -= req;
						count = req;
						j++;
					}
					// invPawn has exactly the amount of thingDef in inventory as reqPawn requires
					else if (inv == req)
					{
						// remove both pawns from their respective lists
						reqPawns.Remove(reqPawn);
						invPawns.Remove(invPawn);
						count = req;
					}
					// invPawn has fewer thingDef in inventory than reqPawn requires
					else
					{
						// remove invPawn from inventory-list and decrease amount of thingDef required for reqPawn 
						reqThingDef[reqPawn] -= inv;
						invPawns.Remove(invPawn);
						count = inv;
					}

					// decrease required and inventory counts
					//Log.Message($"inv {invPawn} ({inv}) >> {count} >> req {reqPawn} ({req})");
					req -= count;
					inv -= count;

					// transfer the required things from invPawn's inventory to reqPawn
					TransferRequired(thingDef, count, reqPawn, invPawn, inventories[invPawn]);

					// stop searching through inventories when all thingDef-requirements for this pawn are met
					if (req == 0)
						break;
				}
			}
		}


		private static void TransferRequired(ThingDef thingDef, int count, Pawn reqPawn, Pawn invPawn, List<Thing> thingsOnInvPawn)
		{
			var remainder = count;
			foreach (var thing in thingsOnInvPawn)
			{
				if (thing.def == thingDef)
				{
					var c = Mathf.Min(remainder, thing.stackCount);
					Log.Message($"Tranferring {c} / {thing.stackCount} of {thing} ({thingDef}) from {thing.holdingOwner.Owner} to {reqPawn}");
					thing.holdingOwner.TryTransferToContainer(thing, reqPawn.inventory.innerContainer, c);

					if (c == thing.stackCount)
						thingsOnInvPawn.Remove(thing);

					remainder -= c;
					if (remainder == 0)
						break;
				}
			}
			if (remainder > 0)
				Log.Error($"TransferThing could not find enough items to transfer for {thingDef} on {invPawn}; {remainder} out of {count} left");
		}
		private static Dictionary<ThingDef, int> GetAllRequired(this Pawn pawn)
		{
			var result = new Dictionary<ThingDef, int>();
			foreach (var entry in pawn.drugs.CurrentPolicy.entriesInt)
			{
				var thingDef = entry.drug;
				var count = entry.takeToInventory;
				if (count > 0)
				{
					if (!result.ContainsKey(thingDef))
						result.Add(thingDef, count);
					else
						result[thingDef] += count;
				}
			}
			foreach (var carry in pawn.inventoryStock.stockEntries)
			{
				var thingDef = carry.Value.thingDef;
				var count = carry.Value.count;
				if (count > 0)
				{
					if (!result.ContainsKey(thingDef))
						result.Add(thingDef, count);
					else
						result[thingDef] += count;
				}
			}
			return result;
		}
	}
}
