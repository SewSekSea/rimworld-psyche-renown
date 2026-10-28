using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Renown
{
    public sealed class ContractQuestRoot : QuestNode
    {
        static QuestScriptDef Native=>DefDatabase<QuestScriptDef>.GetNamedSilentFail("TradeRequest");
        static QuestNode_Sequence Sequence=>Native?.root as QuestNode_Sequence;
        public static bool Supported=>Sequence!=null && Sequence.nodes.Count(n=>n is QuestNode_GetNearbySettlement)==1 && Sequence.nodes.Any(n=>n is QuestNode_TradeRequest_Initiate);
        static bool Seed(Slate slate)
        {
            var map=slate.Get<Map>("map");var target=slate.Get<Settlement>("rnSettlement");var report=slate.Get<FactionKnowledge>("rnReport");
            if(!Supported || GameComponent_Renown.Current?.ContractSettlementEligible(report,map,target)!=true)return false;
            slate.Set("settlement",target);slate.Set("asker",target.Faction.leader);slate.Set("canCaravan",true);return true;
        }
        protected override bool TestRunInt(Slate slate)
        {
            if(!Seed(slate))return false;
            return Sequence.nodes.Where(n=>!(n is QuestNode_GetNearbySettlement)).All(n=>n.TestRun(slate));
        }
        protected override void RunInt()
        {
            if(!Seed(QuestGen.slate))throw new InvalidOperationException("Renown contract target no longer eligible.");
            QuestGen.AddQuestDescriptionRules(Native.questDescriptionRules);
            QuestGen.AddQuestNameRules(Native.questNameRules);
            foreach(var node in Sequence.nodes)if(!(node is QuestNode_GetNearbySettlement))node.Run();
        }
    }
    public partial class GameComponent_Renown
    {
        public Quest CreateContractOffer(FactionKnowledge report,Map map,Settlement settlement)
        {
            if(!ContractSettlementEligible(report,map,settlement) || !ContractQuestRoot.Supported || QuestGen.Working)return null;
            var def=DefDatabase<QuestScriptDef>.GetNamedSilentFail("RN_TradeContract");if(def==null)return null;
            var slate=new Slate();slate.Set("map",map);slate.Set("rnSettlement",settlement);slate.Set("rnReport",report);slate.Set("points",StorytellerUtility.DefaultThreatPointsNow(map));
            if(!def.root.TestRun(slate))return null;
            var quest=QuestGen.Generate(def,slate);
            var parts=quest?.PartsListForReading.OfType<QuestPart_InitiateTradeRequest>().ToArray();
            var part=parts?.Length==1?parts[0]:null;
            if(part==null || part.settlement!=settlement || part.requestedThingDef==null || part.requestedCount<=0 || part.requestDuration<=0)
            {quest?.CleanupQuestParts();return null;}
            Find.QuestManager.Add(quest);return quest;
        }
    }
}
