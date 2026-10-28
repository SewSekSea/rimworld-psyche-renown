using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        // Campaigns distribute genuine evidence. They never manufacture agreement or take ownership of Crew membership.
        public CrewRivalry CampaignFor(Pawn speaker,Pawn listener)
        {
            if(!Conversation(speaker,listener) || !speaker.IsFreeColonist || !listener.IsFreeColonist)return null;
            int now=Find.TickManager.TicksGame;
            return Rivalries.Where(r=>r!=null && r.Warned && r.ObserverLeader!=null && r.OtherLeader!=null &&
                (r.LastCampaign<0 || now-r.LastCampaign>=Policy.Day))
                .OrderBy(r=>r.LastCampaign).ThenBy(r=>r.OtherLeader.thingIDNumber).FirstOrDefault(r=>
                CrewContext.Members(r.ObserverLeader).Contains(speaker) && !CrewContext.Members(r.ObserverLeader).Contains(listener) &&
                RivalryPressure(r.ObserverLeader,r.OtherLeader)>=20 && StoryAbout(speaker,listener,r.OtherLeader)!=null);
        }
        public bool Campaign(Pawn speaker,Pawn listener)
        {
            var rivalry=CampaignFor(speaker,listener);if(rivalry==null)return false;
            if(!ShareAbout(speaker,listener,rivalry.OtherLeader))return false;
            rivalry.LastCampaign=Find.TickManager.TicksGame;
            rivalry.CampaignAccounts=System.Math.Min(1000000,rivalry.CampaignAccounts+1);
            ApplyCampaignView(listener,rivalry.OtherLeader);
            DisplayBridge.Record(speaker.LabelShort+" challenged "+rivalry.OtherLeader.LabelShort+"'s standing in conversation.",
                "A rival Crew member shared a genuine account. The listener judges it independently and may admire the subject instead. One account per directed Crew rivalry per day; trust, distance, evidence and transmission limits still apply. No forced support or membership change.",speaker,listener,speaker.Map);
            return true;
        }
    }
    public class InteractionWorker_Campaign : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.CampaignFor(initiator,recipient)!=null?.15f:0;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Campaign(initiator,recipient);}
    }
}
