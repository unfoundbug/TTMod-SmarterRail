using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using BepInEx.Configuration;
using System.Linq;

namespace RightDrag
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class SmarterTransit : BaseUnityPlugin
    {
        public const string pluginGuid = "SmarterTransit.nhickling.co.uk";
        public const string pluginName = "SmarterTransit";
        public const string pluginVersion = "0.0.0.3";
        private static BepInEx.Logging.ManualLogSource ModLogger;
        private static List<int> activatedSlots = new List<int>();
        public static ConfigEntry<int> EmptySlotsRequired;
        public static ConfigEntry<int> MinimumLoadPercentage;


        public void Awake()
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Logger.LogInfo("SmarterTransit: started");

            Harmony harmony = new Harmony(pluginGuid);
            Logger.LogInfo("SmarterTransit: Fetching patch references");
            {
                Logger.LogInfo("Patching Outbound");
                MethodInfo original = AccessTools.Method(typeof(TransitDepotInstance), "StartPacking", new Type[] { });
                MethodInfo patch = AccessTools.Method(typeof(SmarterTransit), "StartPacking_MyPatch");
                Logger.LogInfo("SmarterTransit: Starting Patch");
                harmony.Patch(original, new HarmonyMethod(patch), null);
                Logger.LogInfo("SmarterTransit: Patched");
            }

            {
                Logger.LogInfo("Patching Inbound");

                var methods = AccessTools.GetDeclaredMethods(typeof(TransitDepotInstance));
                var relevant = methods.First(method => method.Name == "ValidateAddResources" && !method.IsStatic);
                MethodInfo patch = AccessTools.Method(typeof(SmarterTransit), "ValidateAddResources_MyPatch");
                Logger.LogInfo("SmarterTransit: Starting Patch");
                harmony.Patch(relevant, new HarmonyMethod(patch), null);
                Logger.LogInfo("SmarterTransit: Patched");
            }

            EmptySlotsRequired = ((BaseUnityPlugin)this).Config.Bind<int>("Config", "EmptySlotsRequired", 50, new ConfigDescription("Number of EMPTY slots required at the destination before a pack can be sent. (Currently Disabled)", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 55), Array.Empty<object>()));
            MinimumLoadPercentage = ((BaseUnityPlugin)this).Config.Bind<int>("Config", "MinimumLoadPercentage", 100, new ConfigDescription("Minimum percentage of items before a pack will be sent. (0 to always send, 100 to only send full carts)", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 100), Array.Empty<object>()));

            ModLogger = Logger;
        }

        public static bool StartPacking_MyPatch(TransitDepotInstance __instance)
        {

            // If we have a target rail
            
            if(__instance.hasPair)
            {
                ref TransitDepotInstance pairDepot = ref __instance.pairedDepot.Get();
                ref Inventory local = ref __instance.GetInputInventory();
                ref Inventory remote = ref pairDepot.GetOutputInventory();
                var requiredItems = (float)__instance.maxCapacity;

                int req = MinimumLoadPercentage.Value;
                int cur = local.GetTotalResourceCount();

                float perc = ((float)cur / requiredItems) * 100;
                bool isOk = Math.Ceiling(perc) >= req;


                if(!isOk)
                {                    
                    return false;
                }

                if (remote.GetNumberOfEmptySlots() < EmptySlotsRequired.Value)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool ValidateAddResources_MyPatch(TransitDepotInstance __instance, ref bool __result, int resId, int inventoryIndex, out int slotNum, AddResourceValidationType validationType)
        {
            ref Inventory local = ref __instance.GetInputInventory();
            slotNum = -1;

            if (local.GetNumberOfEmptySlots() < EmptySlotsRequired.Value)
            {
                __result = false;
                return false;
            }
            
            return true;
        }
    }
}
