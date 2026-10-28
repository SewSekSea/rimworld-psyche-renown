using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Renown
{
    public static class CampaignContext
    {
        public static readonly Type Provider=AccessTools.TypeByName("Schemer.GameComponent_Schemer");
        public static Pawn Candidate(Pawn organizer)
        {
            if(Provider==null || organizer==null)return null;
            var ledger=AccessTools.Property(Provider,"Current")?.GetValue(null,null);
            var campaign=ledger==null?null:AccessTools.Method(Provider,"ActiveFor")?.Invoke(ledger,new object[]{organizer});
            return campaign==null?null:(Pawn)AccessTools.Field(campaign.GetType(),"Candidate")?.GetValue(campaign);
        }
        public static float Interest(Pawn observer,Pawn candidate)
        {
            var ledger=GameComponent_Renown.Current;
            if(candidate==null || observer==null || observer==candidate || ledger?.Knows(observer,candidate)==null)return 1f;
            var view=ledger.Assess(observer,candidate);
            return Math.Max(.75f,Math.Min(1.25f,1f+(view.Admiration-view.Distrust-view.Threat)/100f));
        }
    }
    [HarmonyPatch]
    public static class CanvassInterestPatch
    {
        static MethodBase TargetMethod()=>AccessTools.Method(AccessTools.TypeByName("Schemer.InteractionWorker_Canvass"),"RandomSelectionWeight");
        static bool Prepare()=>AccessTools.TypeByName("Schemer.InteractionWorker_Canvass")!=null;
        static void Postfix(Pawn initiator,Pawn recipient,ref float __result,bool __runOriginal)
        {
            if(!__runOriginal || __result<=0)return;
            float weight=__result;
            ObservationGuard.Run("campaign interest",()=>weight*=CampaignContext.Interest(recipient,CampaignContext.Candidate(initiator)));
            __result=weight;
        }
    }
    [HarmonyPatch]
    public static class CampaignAccountPatch
    {
        static bool Prepare()=>CampaignContext.Provider!=null;
        static MethodBase TargetMethod()=>AccessTools.Method(CampaignContext.Provider,"Approach");
        public sealed class Attempt {public Pawn Candidate;public float Interest;}
        static void Prefix(object __instance,Pawn organizer,Pawn recipient,out Attempt __state)
        {
            Attempt attempt=null;
            ObservationGuard.Run("campaign assessment",()=>
            {
                if(!(bool)AccessTools.Method(CampaignContext.Provider,"CanApproach").Invoke(__instance,new object[]{organizer,recipient}))return;
                var candidate=CampaignContext.Candidate(organizer);
                if(candidate!=null)attempt=new Attempt{Candidate=candidate,Interest=CampaignContext.Interest(recipient,candidate)};
            });
            __state=attempt;
        }
        static void Postfix(Pawn organizer,Pawn recipient,bool __result,Attempt __state,bool __runOriginal)
        {
            if(!__runOriginal || __state==null)return;
            ObservationGuard.Run("campaign account",()=>
            {
                var ledger=GameComponent_Renown.Current;
                var story=ledger?.Story(organizer,recipient);
                bool shared=story?.Maker==__state.Candidate && ledger.Share(organizer,recipient);
                DisplayBridge.Record(recipient.LabelShort+(__result?" supported ":" declined to support ")+__state.Candidate.LabelShort+" after canvassing.",
                    "Renown conversation-interest multiplier before the approach: "+__state.Interest.ToString("0.00")+" (0.75-1.25). "+
                    (shared?"An independently sourced candidate account was shared afterward. ":"No new candidate account was shared. ")+
                    "Schemer alone applied its unchanged trust/support rules. Interest affects conversation frequency, not support thresholds or leadership.",organizer,recipient,organizer.Map);
            });
        }
    }
}
