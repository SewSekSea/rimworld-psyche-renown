using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Renown
{
    public sealed class CrewRivalry : IExposable
    {
        public Pawn ObserverLeader,OtherLeader;
        public int Pressure,Updated;
        public bool Warned;
        public int LastCampaign=-1,CampaignAccounts;
        public void ExposeData()
        {
            Scribe_References.Look(ref ObserverLeader,"observerLeader");
            Scribe_References.Look(ref OtherLeader,"otherLeader");
            Scribe_Values.Look(ref Pressure,"pressure");Scribe_Values.Look(ref Updated,"updated");
            Scribe_Values.Look(ref Warned,"warned");
            Scribe_Values.Look(ref LastCampaign,"lastCampaign",-1);Scribe_Values.Look(ref CampaignAccounts,"campaignAccounts");
        }
    }
    public partial class GameComponent_Renown
    {
        public List<CrewRivalry> Rivalries=new List<CrewRivalry>();
        public string RivalryDetails(Pawn focus)
        {
            var lines=Rivalries.Where(r=>r!=null && r.ObserverLeader!=null && r.OtherLeader!=null &&
                (r.ObserverLeader==focus || r.OtherLeader==focus || CrewContext.Members(r.ObserverLeader).Contains(focus)))
                .Select(r=>r.ObserverLeader.LabelShort+"'s crew toward "+r.OtherLeader.LabelShort+": "+RivalryPressure(r.ObserverLeader,r.OtherLeader)+"/100 current tension. Campaign accounts shared: "+r.CampaignAccounts+".").ToArray();
            return lines.Length==0?"No retained Crew rivalry involving this pawn.":string.Join("\n",lines);
        }
        // Directed: one crew can distrust another without the feeling being mutual.
        public int RivalryPressure(Pawn leader,Pawn other)
        {
            if(leader==null || other==null || leader==other || leader.Map==null || leader.Map!=other.Map)return 0;
            var members=CrewContext.Members(leader);var opponents=CrewContext.Members(other);
            if(members.Length<3 || opponents.Length<3 || members.Intersect(opponents).Any())return 0;
            var informed=members.Where(p=>Knows(p,other)!=null).ToArray();
            if(informed.Length<=members.Length/2)return 0;
            // Uninformed members contribute zero, not assumed agreement.
            return Math.Min(100,Math.Max(0,(int)Math.Round(informed.Sum(p=>
            {var a=Assess(p,other);return Math.Max(0,a.Threat+a.Distrust-a.Admiration);})/(double)members.Length)));
        }
        public void RefreshRivalries()
        {
            int now=Find.TickManager.TicksGame;
            var leaders=Find.Maps.SelectMany(m=>m.mapPawns.FreeColonistsSpawned)
                .Where(p=>CrewContext.Members(p).Length>=3).OrderBy(p=>p.thingIDNumber).ToArray();
            var valid=new HashSet<Pawn>(leaders);
            Rivalries.RemoveAll(r=>r==null || !valid.Contains(r.ObserverLeader) || !valid.Contains(r.OtherLeader) || r.ObserverLeader.Map!=r.OtherLeader.Map);
            foreach(var leader in leaders)foreach(var other in leaders)
            {
                if(leader==other || leader.Map!=other.Map)continue;
                int pressure=RivalryPressure(leader,other);
                var record=Rivalries.FirstOrDefault(r=>r.ObserverLeader==leader && r.OtherLeader==other);
                if(record==null)
                {
                    if(pressure<20 || Rivalries.Count>=128)continue;
                    record=new CrewRivalry{ObserverLeader=leader,OtherLeader=other};Rivalries.Add(record);
                }
                record.Pressure=pressure;record.Updated=now;
                if(!record.Warned && pressure>=20)
                {
                    record.Warned=true;
                    DisplayBridge.Record(leader.LabelShort+"'s crew is wary of "+other.LabelShort+"'s standing.",
                        "Known accounts and current opinions produce "+pressure+"/100 tension. A majority holds evidence; uninformed members are not counted as supporters. No membership, leadership or jobs changed.",leader,other,leader.Map);
                }
                else if(record.Warned && pressure<10)
                {
                    record.Warned=false;
                    DisplayBridge.Record("Tension eased between "+leader.LabelShort+"'s crew and "+other.LabelShort+".",
                        "Current valid accounts or relationships no longer sustain the warning. This does not erase memories or force forgiveness.",leader,other,leader.Map);
                }
            }
            Rivalries.RemoveAll(r=>!r.Warned && r.Pressure==0 && (r.LastCampaign<0 || now-r.LastCampaign>=Policy.Day));
        }
        void ExposeRivalries()
        {
            Scribe_Collections.Look(ref Rivalries,"renownCrewRivalries",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && Rivalries==null)Rivalries=new List<CrewRivalry>();
        }
    }
}
