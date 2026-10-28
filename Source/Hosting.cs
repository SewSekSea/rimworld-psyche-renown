using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class HostingVisit : IExposable
    {
        public Faction Faction;
        public Map Map;
        public Pawn Subject;
        public List<Pawn> Guests=new List<Pawn>(),Returned=new List<Pawn>();
        public int Started,LastSample=-1,Warning=-1,Closed=-1,Goodwill;
        public bool Adverse;
        public string Conditions="",Outcome="active";
        public void ExposeData()
        {
            Scribe_References.Look(ref Faction,"faction");Scribe_References.Look(ref Map,"map");Scribe_References.Look(ref Subject,"subject");
            Scribe_Collections.Look(ref Guests,"guests",LookMode.Reference);Scribe_Collections.Look(ref Returned,"returned",LookMode.Reference);
            Scribe_Values.Look(ref Started,"started");Scribe_Values.Look(ref LastSample,"lastSample",-1);Scribe_Values.Look(ref Warning,"warning",-1);
            Scribe_Values.Look(ref Closed,"closed",-1);Scribe_Values.Look(ref Goodwill,"goodwill");Scribe_Values.Look(ref Adverse,"adverse");
            Scribe_Values.Look(ref Conditions,"conditions","");Scribe_Values.Look(ref Outcome,"outcome","active");
            if(Scribe.mode==LoadSaveMode.PostLoadInit){Guests=Guests??new List<Pawn>();Returned=Returned??new List<Pawn>();}
        }
    }
    public partial class GameComponent_Renown
    {
        public List<HostingVisit> Hosting=new List<HostingVisit>();
        public HostingVisit TrackHosting(Faction faction,Map map,Pawn subject,IEnumerable<Pawn> guests)
        {
            if(RenownMod.Settings?.Hosting!=true || faction==null || map==null || faction.IsPlayer || faction.HostileTo(Faction.OfPlayer))return null;
            var party=guests?.Where(p=>p!=null && p.RaceProps.Humanlike && !p.Dead && p.Spawned && p.Map==map && p.Faction==faction).Distinct().ToList();
            if(party==null || party.Count==0 || Hosting.Any(v=>v.Closed<0 && v.Guests.Intersect(party).Any()))return null;
            if(Hosting.Count>=16){var old=Hosting.FirstOrDefault(v=>v.Closed>=0);if(old==null)return null;Hosting.Remove(old);}
            var visit=new HostingVisit{Faction=faction,Map=map,Subject=subject,Guests=party,Started=Find.TickManager.TicksGame};Hosting.Add(visit);
            DisplayBridge.Record("Delegation hosting observations started.","Only this generated party is tracked. Food below20%, exhaustion outside shelter, bleeding/downing or captivity prompt a six-hour warning. At departure, a returning member can carry a bounded report; native consequences still apply.",subject,null,map);
            return visit;
        }
        string HostingConditions(HostingVisit visit)
        {
            var guests=visit.Guests.Where(p=>p!=null && !visit.Returned.Contains(p)).ToArray();
            var present=guests.Where(p=>!p.Dead && p.Spawned && p.Map==visit.Map).ToArray();
            var issues=new List<string>();
            if(present.Any(p=>p.needs?.food!=null && p.needs.food.CurLevelPercentage<.2f))issues.Add("food below20%");
            if(present.Any(p=>p.needs?.rest!=null && p.needs.rest.CurLevelPercentage<.3f && !p.Position.Roofed(visit.Map)))issues.Add("exhausted without shelter");
            if(guests.Any(p=>p.Dead || p.IsPrisoner) || present.Any(p=>p.Downed || p.health.hediffSet.hediffs.Any(h=>h.Bleeding)))issues.Add("injury, death or captivity observed");
            return string.Join(", ",issues);
        }
        void CloseHosting(HostingVisit visit,string outcome,int requested)
        {
            if(visit.Closed>=0)return;
            visit.Closed=Find.TickManager.TicksGame;visit.Outcome=outcome;
            if(requested!=0 && visit.Faction!=null && !visit.Faction.defeated && !visit.Faction.HostileTo(Faction.OfPlayer))
            {
                int before=Faction.OfPlayer.GoodwillWith(visit.Faction);
                int delta=requested<0?-Math.Min(-requested,Math.Max(0,before)):requested;
                if(delta!=0)Faction.OfPlayer.TryAffectGoodwillWith(visit.Faction,delta,false,false);
                visit.Goodwill=Faction.OfPlayer.GoodwillWith(visit.Faction)-before;
            }
            DisplayBridge.Record("Delegation hosting: "+outcome+".","Returning members: "+visit.Returned.Count+". Observed conditions, not inferred blame. Actual Renown goodwill change: "+visit.Goodwill+". Native incident consequences are independent.",visit.Subject,null,visit.Map);
        }
        public void RefreshHosting()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var visit in Hosting.Where(v=>v.Closed<0).ToArray())
            {
                if(RenownMod.Settings?.Hosting!=true){CloseHosting(visit,"tracking disabled",0);continue;}
                if(visit.Map==null || visit.Faction==null || visit.Faction.defeated || visit.Faction.HostileTo(Faction.OfPlayer)){CloseHosting(visit,"visit interrupted",0);continue;}
                if(now-visit.Started>=7*Policy.Day){CloseHosting(visit,"observation expired",0);continue;}
                bool present=visit.Guests.Any(p=>p!=null && !p.Dead && p.Spawned && p.Map==visit.Map && !visit.Returned.Contains(p));
                if(!present)
                {
                    bool qualified=visit.LastSample-visit.Started>=Policy.Day/4;
                    CloseHosting(visit,visit.Returned.Count==0?"no returning witness":!qualified?"short visit":visit.Adverse?"sustained hosting concerns recorded":"visit concluded without sustained concerns",
                        visit.Returned.Count==0 || !qualified?0:visit.Adverse?-2:1);continue;
                }
                if(visit.LastSample>=0 && now-visit.LastSample<2500)continue;
                visit.LastSample=now;visit.Conditions=HostingConditions(visit);
                if(visit.Conditions!="")
                {
                    if(visit.Warning<0)
                    {
                        visit.Warning=now;
                        Messages.Message("Renown delegation concern: "+visit.Conditions+". Six game hours to resolve; inspect the Reputation view.",visit.Subject,MessageTypeDefOf.CautionInput);
                        DisplayBridge.Record("Delegation concern: "+visit.Conditions+".","Six-hour grace before a sustained-concern report. Improve the observed conditions; this is not a claim about who caused them.",visit.Subject,null,visit.Map);
                    }
                    else if(now-visit.Warning>=Policy.Day/4)visit.Adverse=true;
                }
                else if(visit.Warning>=0)
                {
                    visit.Warning=-1;
                    DisplayBridge.Record("Delegation conditions improved.",visit.Adverse?"Immediate concerns resolved; earlier sustained concerns remain in the visit report.":"Resolved within grace; no adverse report earned.",visit.Subject,null,visit.Map);
                }
            }
        }
        public void HostingDeparture(Pawn member)
        {
            if(member==null || member.Dead || member.IsPrisoner)return;
            foreach(var visit in Hosting.Where(v=>v.Closed<0 && v.Guests.Contains(member) && member.Faction==v.Faction))
                if(!visit.Returned.Contains(member))visit.Returned.Add(member);
            RefreshHosting();
        }
        public string HostingDetails(Map map)
        {
            int now=Find.TickManager.TicksGame;
            return string.Join("\n",Hosting.Where(v=>v.Map==map).Take(16).Select(v=>(v.Faction?.Name??"unavailable faction")+" | "+v.Outcome+
                " | returned "+v.Returned.Count+"/"+v.Guests.Count+(v.Closed>=0?" | goodwill "+v.Goodwill:
                " | "+(v.Conditions==""?"no current concern":v.Conditions)+(v.Warning>=0?" | grace remaining "+(Math.Max(0,v.Warning+Policy.Day/4-now)/2500f).ToString("0.0")+"h":""))));
        }
        void ExposeHosting()
        {
            Scribe_Collections.Look(ref Hosting,"renownHosting",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)Hosting=(Hosting??new List<HostingVisit>()).Where(v=>v!=null).Take(16).ToList();
        }
    }
}
