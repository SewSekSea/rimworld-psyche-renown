using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        int lastContractAttempt=-1,lastContractSuccess=-1;
        public int LastContractAttempt=>lastContractAttempt;
        public int LastContractSuccess=>lastContractSuccess;
        public string ContractDetails()
        {
            var quests=Find.QuestManager.QuestsListForReading.Where(q=>(q.root?.defName=="RN_TradeContract" || q.root?.defName=="RN_MercenaryContract") && !q.Historical).ToArray();
            if(quests.Length==0)return "";
            return "Colony-wide contracts (all maps): "+quests.Length+"\n"+string.Join("\n",quests.Take(16).Select(q=>(q.root.defName=="RN_MercenaryContract"?"Mercenary: ":"Trade: ")+q.name+" | "+q.State+(q.dismissed?" | dismissed":"")))+"\nReview objectives, risks, rewards and deadlines in Quests. Disabling new offers does not cancel these.";
        }
        public bool TryContract(Map map)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.Contracts!=true || now<7*Policy.Day || lastContractAttempt>=0 && now-lastContractAttempt<30*Policy.Day)return false;
            foreach(var report in Abroad.Where(r=>ContractEvidenceEligible(r,map)))
            {
                var target=ContractSettlement(report,map);if(target==null)continue;
                lastContractAttempt=now;
                var quest=CreateContractOffer(report,map,target);if(quest==null)return false;
                lastContractSuccess=now;
                QuestUtility.SendLetterQuestAvailable(quest,"Reports of "+report.Maker.LabelShort+" reached "+report.Faction.Name);
                DisplayBridge.Record("A reputation-backed trade contract is available from "+target.Label+".","Fresh evidence about "+report.Maker.LabelShort+" reached this faction. Review the native Quests tab before accepting. Delivery, rewards, expiry and the native +12 fulfilment goodwill use ordinary trade-request rules. One attempt per thirty days globally, including failures and ignored offers. Disabling future offers does not cancel accepted contracts.",report.Maker,null,map);
                return true;
            }
            return false;
        }
        void RefreshContracts()
        {
            if(RenownMod.Settings?.Contracts!=true)return;
            foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryContract(map))break;
        }
        void ExposeContracts()
        {
            Scribe_Values.Look(ref lastContractAttempt,"renownLastContractAttempt",-1);
            Scribe_Values.Look(ref lastContractSuccess,"renownLastContractSuccess",-1);
        }
    }
}
