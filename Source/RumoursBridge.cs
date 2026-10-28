using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    // Only attach an already-held, independently sourced deed to a genuine gossip interaction.
    // Never import secrets, invented claims or rumours as new deed evidence.
    [HarmonyPatch]
    public static class RumoursBridge
    {
        static readonly Type Worker=AccessTools.TypeByName("Rumours.InteractionWorker_Rumour");
        static readonly MethodInfo Target=Worker==null?null:AccessTools.Method(Worker,"Interacted");
        static bool Prepare()=>Target!=null;
        static MethodBase TargetMethod()=>Target;
        public static bool Available=>Target!=null;
        static void Prefix(Pawn recipient,out HashSet<Thought_Memory> __state)
        {__state=new HashSet<Thought_Memory>(recipient?.needs?.mood?.thoughts?.memories?.Memories??new List<Thought_Memory>());}
        static void Postfix(Pawn initiator,Pawn recipient,HashSet<Thought_Memory> __state,bool __runOriginal)
        {
            if(!__runOriginal || __state==null)return;
            ObservationGuard.Run("Rumours source bridge",()=>
            {
                var memories=recipient?.needs?.mood?.thoughts?.memories?.Memories;
                if(memories==null)return;
                foreach(var memory in memories.Where(m=>!__state.Contains(m)).ToArray())
                {
                    if(memory.GetType().FullName!="Rumours.Thought_Rumour" || !(memory is Thought_MemorySocial social))continue;
                    var invented=AccessTools.Field(memory.GetType(),"invented");
                    if(invented==null || (bool)invented.GetValue(memory))continue;
                    if(Transfer(initiator,recipient,social.otherPawn,false))break;
                }
            });
        }
        public static bool Transfer(Pawn speaker,Pawn listener,Pawn subject,bool invented)
        {
            if(invented || subject==null)return false;
            var ledger=GameComponent_Renown.Current;
            var account=ledger?.Story(speaker,listener);
            // Existing Renown age, trust, distance, provenance, depth and duplicate gates still apply.
            if(account==null || account.Maker!=subject)return false;
            return ledger.Share(speaker,listener);
        }
    }
}
