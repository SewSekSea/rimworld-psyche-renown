using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    [HarmonyPatch(typeof(Frame),nameof(Frame.CompleteConstruction))]
    public static class ConstructionObservation
    {
        public sealed class Snapshot
        {
            public Map Map;
            public IntVec3 Position;
            public ThingDef Def;
            public HashSet<int> Existing;
        }
        static bool warned;
        static void Prefix(Frame __instance,out Snapshot __state)
        {
            __state=null;
            if(!__instance.Spawned || !(__instance.def.entityDefToBuild is ThingDef def) || def.category!=ThingCategory.Building)return;
            __state=new Snapshot{Map=__instance.Map,Position=__instance.Position,Def=def,Existing=new HashSet<int>(__instance.Position.GetThingList(__instance.Map).Select(t=>t.thingIDNumber))};
        }
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Frame __instance,Pawn worker,Snapshot __state,bool __runOriginal)
        {
            if(!__runOriginal || __state==null || !__instance.Destroyed || worker?.Map!=__state.Map)return;
            try
            {
                var results=__state.Position.GetThingList(__state.Map).Where(t=>t.def==__state.Def && !__state.Existing.Contains(t.thingIDNumber)).ToArray();
                if(results.Length==1)GameComponent_Renown.Current?.ObserveFinishedProduct(results[0],worker);
            }
            catch(Exception e){if(!warned)Log.Warning("[Renown] Construction observation unavailable: "+e.GetType().Name);warned=true;}
        }
    }
}
