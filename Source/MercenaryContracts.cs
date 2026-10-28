using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Renown
{
    public sealed class MercenaryQuestRoot : QuestNode
    {
        static QuestScriptDef Native=>DefDatabase<QuestScriptDef>.GetNamedSilentFail("OpportunitySite_BanditCamp");
        static QuestNode_Sequence Sequence=>Native?.root as QuestNode_Sequence;
        public static bool Supported=>Sequence!=null && Sequence.nodes.Count(n=>n is QuestNode_GetPawn)==1 && Sequence.nodes.Any(n=>n is QuestNode_SpawnWorldObjects);
        static bool Seed(Slate slate)
        {
            var report=slate.Get<FactionKnowledge>("rnReport");var map=slate.Get<Map>("map");
            if(!Supported || GameComponent_Renown.Current?.MercenaryEligible(report,map)!=true)return false;
            slate.Set("asker",report.Faction.leader);return true;
        }
        protected override bool TestRunInt(Slate slate)=>Seed(slate) && Sequence.nodes.Where(n=>!(n is QuestNode_GetPawn)).All(n=>n.TestRun(slate));
        protected override void RunInt()
        {
            if(!Seed(QuestGen.slate))throw new InvalidOperationException("Renown mercenary sponsor no longer eligible.");
            QuestGen.AddQuestDescriptionRules(Native.questDescriptionRules);QuestGen.AddQuestNameRules(Native.questNameRules);
            foreach(var node in Sequence.nodes)if(!(node is QuestNode_GetPawn))node.Run();
        }
    }
    public partial class GameComponent_Renown
    {
        int lastMercenaryAttempt=-1,lastMercenarySuccess=-1;
        public int LastMercenaryAttempt=>lastMercenaryAttempt;
        public int LastMercenarySuccess=>lastMercenarySuccess;
        public bool MercenaryEligible(FactionKnowledge report,Map map)=>ContractEvidenceEligible(report,map) && map.Tile.Valid && map.Tile.LayerDef.SurfaceTiles && report.Faction.leader!=null && !report.Faction.leader.Dead && !report.Faction.def.permanentEnemy;
        public Quest CreateMercenaryOffer(FactionKnowledge report,Map map)
        {
            if(!MercenaryEligible(report,map) || !MercenaryQuestRoot.Supported || QuestGen.Working)return null;
            var def=DefDatabase<QuestScriptDef>.GetNamedSilentFail("RN_MercenaryContract");if(def==null)return null;
            var slate=new Slate();slate.Set("map",map);slate.Set("rnReport",report);slate.Set("points",Math.Max(350f,StorytellerUtility.DefaultThreatPointsNow(map)));
            if(!def.root.TestRun(slate))return null;
            slate=new Slate();slate.Set("map",map);slate.Set("rnReport",report);slate.Set("points",Math.Max(350f,StorytellerUtility.DefaultThreatPointsNow(map)));
            var quest=QuestGen.Generate(def,slate);
            var sites=quest?.PartsListForReading.OfType<QuestPart_SpawnWorldObject>().Select(p=>p.worldObject).OfType<Site>().ToArray();
            if(sites==null || sites.Length!=1 || sites[0].Faction==null || !sites[0].Faction.HostileTo(Faction.OfPlayer) || !sites[0].Faction.HostileTo(report.Faction))
            {quest?.CleanupQuestParts();return null;}
            Find.QuestManager.Add(quest);return quest;
        }
        public bool TryMercenary(Map map)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.MercenaryContracts!=true || now<7*Policy.Day || lastMercenaryAttempt>=0 && now-lastMercenaryAttempt<30*Policy.Day)return false;
            if(Find.QuestManager.QuestsListForReading.Any(q=>q.root?.defName=="RN_MercenaryContract" && !q.Historical))return false;
            var report=Abroad.FirstOrDefault(r=>MercenaryEligible(r,map));if(report==null)return false;
            lastMercenaryAttempt=now;var quest=CreateMercenaryOffer(report,map);if(quest==null)return false;
            lastMercenarySuccess=now;
            QuestUtility.SendLetterQuestAvailable(quest,"Reports of "+report.Maker.LabelShort+" attracted a mercenary offer from "+report.Faction.Name);
            DisplayBridge.Record("A reputation-backed bandit-camp contract is available.","Review enemies, travel, rewards and deadlines in Quests before accepting. Native combat and quest outcomes apply. One attempt per thirty days globally, including failures and ignored offers; one outstanding contract at a time. Disabling new offers does not cancel an existing quest. Reputation is not a guarantee of combat ability.",report.Maker,null,map);
            return true;
        }
        void RefreshMercenaryContracts()
        {
            if(RenownMod.Settings?.MercenaryContracts!=true)return;
            foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryMercenary(map))break;
        }
        void ExposeMercenaryContracts(){Scribe_Values.Look(ref lastMercenaryAttempt,"renownLastMercenaryAttempt",-1);Scribe_Values.Look(ref lastMercenarySuccess,"renownLastMercenarySuccess",-1);}
    }
}
