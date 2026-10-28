using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public bool ProtestGroupValid(CrewRequest r)
        {
            if(r==null || r.Leader==null || r.Members==null || r.Members.Count<3)return false;
            var members=CrewContext.Members(r.Leader);
            return members.Length==r.Members.Count && !members.Except(r.Members).Any() && RequestNeeds.Average(members,r.Kind)>=0;
        }
        void EndProtest(CrewRequest r,string reason)
        {
            r.ProtestClosed=Find.TickManager.TicksGame;r.ProtestOutcome=reason;
            foreach(var p in r.Members.Where(p=>p?.needs?.mood!=null))
                if(!Requests.Any(other=>other!=r && other.ProtestStarted>=0 && other.ProtestClosed<0 && other.Members.Contains(p)))
                    p.needs.mood.thoughts.memories.RemoveMemoriesOfDef(DefDatabase<ThoughtDef>.GetNamed("RN_CrewProtest"));
            DisplayBridge.Record("Crew protest ended: "+reason+".","No work orders or leadership changed. The protest memory was removed where no other active protest applies.",r.Leader,null,r.Leader?.Map);
        }
        public void RefreshProtests()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var r in Requests.Where(r=>r!=null && r.Outcome=="unmet" && r.ProtestClosed<0).ToArray())
            {
                if(r.ProtestWarnedAt<0)
                {
                    if(RenownMod.Settings?.CrewProtests!=true || !r.Warned || r.Closed<0 || now<r.Closed || now-r.Closed>=Policy.Day || !ProtestGroupValid(r))continue;
                    if(RequestNeeds.Average(r.Members,r.Kind)>=.6f)continue;
                    r.ProtestWarnedAt=now;
                    r.StrikeAnnounced=RenownMod.Settings.CrewStrikes;
                    r.SabotageAnnounced=RenownMod.Settings.CrewSabotage;
                    r.SplinterAnnounced=RenownMod.Settings.CrewSplintering;
                    string text=r.Leader.LabelShort+"'s crew may protest in one day unless average "+r.Kind+" reaches 60%.";
                    if(r.StrikeAnnounced)text+=" The protest may also pause new automatic sculpture, apparel and smithing jobs for at most one day; emergency and prioritized work remain available.";
                    if(r.SabotageAnnounced)text+=" If this protest ends unresolved, a separate one-day warning may threaten one repairable production-bench breakdown.";
                    if(r.SplinterAnnounced)text+=" If unresolved, a crew of six or more may receive a separate two-day warning of a temporary relationship fracture between dissenters and supporters.";
                    Messages.Message("Renown: "+text,r.Leader,MessageTypeDefOf.NeutralEvent);
                    DisplayBridge.Record(text,"Optional protest: -3 mood for at most one day, replacing the unmet-request memory. A limited production strike applies only if included in this notice and still enabled. Sabotage requires its separate option and warning after this protest. Improving needs, a changed crew or disabling protests cancels it.",r.Leader,null,r.Leader.Map);
                    continue;
                }
                if(RenownMod.Settings?.CrewProtests!=true){EndProtest(r,"option disabled");continue;}
                if(!ProtestGroupValid(r)){EndProtest(r,"group changed");continue;}
                if(RequestNeeds.Average(r.Members,r.Kind)>=.6f){EndProtest(r,"needs improved");continue;}
                if(r.ProtestStarted<0)
                {
                    if(now-r.ProtestWarnedAt<Policy.Day)continue;
                    r.ProtestStarted=now;r.ProtestOutcome="protesting";
                    foreach(var p in r.Members)
                    {
                        p.needs.mood.thoughts.memories.RemoveMemoriesOfDef(DefDatabase<ThoughtDef>.GetNamed("RN_RequestUnmet"));
                        p.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RN_CrewProtest"));
                    }
                    Messages.Message("Renown: "+r.Leader.LabelShort+"'s crew is protesting unmet "+r.Kind+" needs for at most one day.",r.Leader,MessageTypeDefOf.NeutralEvent);
                    DisplayBridge.Record("Crew protest began over "+r.Kind+".","Time-limited -3 mood. Restore average needs to 60% to end it early. Limited production strike: "+(r.StrikeAnnounced && RenownMod.Settings.CrewStrikes?"announced and enabled":"not active")+". Emergency and prioritized work remain available.",r.Leader,null,r.Leader.Map);
                }
                else if(now-r.ProtestStarted>=Policy.Day)EndProtest(r,"time limit reached");
            }
        }
        public string ProtestDetails(CrewRequest r)
        {
            if(r.ProtestWarnedAt<0)return "No protest notice.";
            if(r.ProtestClosed>=0)return "Protest closed: "+r.ProtestOutcome+".";
            int end=(r.ProtestStarted<0?r.ProtestWarnedAt:r.ProtestStarted)+Policy.Day;
            return (r.ProtestStarted<0?"Protest warning":"Protesting")+" | "+(System.Math.Max(0,end-Find.TickManager.TicksGame)/60000f).ToString("0.0")+" days remaining. Restore average "+r.Kind+" to 60% to resolve."+
                (r.StrikeAnnounced?" Limited production strike announced; currently "+(RenownMod.Settings.CrewStrikes && RenownMod.Settings.CrewProtests?"enabled":"disabled")+". Emergency and prioritized work exempt.":"");
        }
    }
}
