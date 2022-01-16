﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CustomPatches
{
	[StaticConstructorOnStartup]
	public static class HarmonyPatches
	{
		static HarmonyPatches()
		{
			Harmony harmony = new Harmony("syrus.custompatches");

			harmony.Patch(
				typeof(CaravanEnterMapUtility).GetMethod("Enter", 
				new Type[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) }),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.CaravanEnterMapUtility_Enter_Prefix)));
			harmony.Patch(
				typeof(CaravanEnterMapUtility).GetMethod("Enter", 
				new Type[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.CaravanEnterMapUtility_Enter_Postfix)));
        }


        public static void CaravanEnterMapUtility_Enter_Prefix(Caravan caravan, out List<Pawn> __state)
        {
            __state = new List<Pawn>();

            if (caravan != null)
            {
                // save a list of all pawns that were in the caravan
                foreach (var pawn in caravan.pawns)
                    __state.Add(pawn);
            }
        }
        public static void CaravanEnterMapUtility_Enter_Postfix(List<Pawn> __state)
        {
            if (!(__state?.Count > 0))
                return;

            // check for missing sidearms and transfer ones picked up by other pawns back to the correct inventory
            CaravanDistributeItems.DistributeThingsForPoliciesAndCarry(__state);
        }
    }
}
