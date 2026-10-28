using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public bool TryPublishReporterNews(PurposeVisit visit)
        {
            if(visit?.Faction==null || visit.Faction.defeated || visit.Faction.deactivated || visit.Faction.HostileTo(Faction.OfPlayer))return false;
            if(RenownMod.Settings?.Reporters!=true || RenownMod.Settings?.Hosting!=true || visit==null || visit.Kind!="reporter" || visit.NewsOutcome!="none" || visit.Closed<0 || visit.Outcome!="party departed" || visit.Collected<1 || !visit.Returned.Any(p=>p!=null && !p.Dead && !p.IsPrisoner && p.Faction==visit.Faction))return false;
            var hosting=Hosting.LastOrDefault(h=>h.Faction==visit.Faction && h.Map==visit.Map && h.Closed>=0 && h.Guests.Intersect(visit.Guests).Any() && h.Returned.Intersect(visit.Returned).Any());
            if(hosting==null || (hosting.Outcome!="sustained hosting concerns recorded" && hosting.Outcome!="visit concluded without sustained concerns"))return false;
            int now=Find.TickManager.TicksGame;
            if(now-visit.Closed<0 || now-visit.Closed>2500 || lastReporterNews>=0 && now-lastReporterNews<30*Policy.Day)return false;
            visit.NewsOutcome=hosting.Adverse?"sustained concerns published":"visit report published";lastReporterNews=now;
            var changes=new List<string>();
            foreach(var faction in Find.FactionManager.AllFactionsListForReading.Where(f=>f!=null && !f.IsPlayer && f!=visit.Faction && !f.Hidden && !f.defeated && !f.deactivated && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer)).ToArray())
            {
                int before=Faction.OfPlayer.GoodwillWith(faction);
                int requested=hosting.Adverse?-Math.Min(1,Math.Max(0,before)):1;
                if(requested!=0)Faction.OfPlayer.TryAffectGoodwillWith(faction,requested,false,false);
                int actual=Faction.OfPlayer.GoodwillWith(faction)-before;
                changes.Add(faction.Name+": "+(actual>0?"+":"")+actual);
            }
            visit.NewsChanges=string.Join("; ",changes);
            Find.LetterStack.ReceiveLetter("Renown: reporter bulletin",(visit.Faction?.Name??"Returning reporters")+" distributed a report about this colony: "+visit.NewsOutcome+". These are recorded hosting conditions, not proof of who caused them.\n\nActual goodwill changes: "+(changes.Count==0?"No other eligible factions.":visit.NewsChanges)+"\n\nThe originating faction's hosting report is separate and is not charged twice. One bulletin per thirty days. Renown reductions do not cross below zero; native events remain independent.",hosting.Adverse?LetterDefOf.NegativeEvent:LetterDefOf.PositiveEvent,visit.Map?.Parent);
            DisplayBridge.Record("Reporter bulletin: "+visit.NewsOutcome+".",visit.NewsChanges+". Derived from returning witnesses and recorded hosting conditions; no pawn knowledge or memories are fabricated.",visit.Subject,null,visit.Map);
            return true;
        }
    }
}
