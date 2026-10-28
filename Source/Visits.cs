using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Renown
{
    public class RenownSettings : ModSettings
    {
        public bool TraderVisits,Diplomacy,CrewProtests,CrewStrikes,Delegations,Recruits,Hosting,Contracts,Emissaries;
        public bool EmissaryTrading;
        public bool MilitaryAid,MercenaryContracts,Pilgrims,Reporters;
        public bool CrewPersuasion;
        public bool CrewSabotage;
        public bool CrewSplintering;
        public bool TargetedAttacks;
        public bool BountyHunts;
        public bool CaravanThreats;
        public bool WealthPrestige;
        public override void ExposeData(){Scribe_Values.Look(ref WealthPrestige,"renownWealthPrestige",false);Scribe_Values.Look(ref CaravanThreats,"renownCaravanThreats",false);Scribe_Values.Look(ref BountyHunts,"renownBountyHunts",false);Scribe_Values.Look(ref TargetedAttacks,"renownTargetedAttacks",false);Scribe_Values.Look(ref CrewSplintering,"renownCrewSplintering",false);Scribe_Values.Look(ref CrewSabotage,"renownCrewSabotage",false);Scribe_Values.Look(ref CrewPersuasion,"renownCrewPersuasion",false);Scribe_Values.Look(ref Pilgrims,"renownPilgrims",false);Scribe_Values.Look(ref Reporters,"renownReporters",false);Scribe_Values.Look(ref MercenaryContracts,"renownMercenaryContracts",false);Scribe_Values.Look(ref MilitaryAid,"renownMilitaryAid",false);Scribe_Values.Look(ref EmissaryTrading,"renownEmissaryTrading",false);Scribe_Values.Look(ref Emissaries,"renownEmissaries",false);Scribe_Values.Look(ref Contracts,"renownContracts",false);Scribe_Values.Look(ref Hosting,"renownHostingEnabled",false);Scribe_Values.Look(ref Recruits,"renownRecruits",false);Scribe_Values.Look(ref Delegations,"renownDelegations",false);Scribe_Values.Look(ref TraderVisits,"renownTraderVisits",false);Scribe_Values.Look(ref Diplomacy,"renownDiplomacy",false);Scribe_Values.Look(ref CrewProtests,"renownCrewProtests",false);Scribe_Values.Look(ref CrewStrikes,"renownCrewStrikes",false);}
    }
    public class RenownMod : Mod
    {
        public static RenownSettings Settings;
        public RenownMod(ModContentPack content):base(content){Settings=GetSettings<RenownSettings>();}
        public override string SettingsCategory()=>"Renown";
        public override void DoSettingsWindowContents(Rect rect)
        {
            var list=new Listing_Standard();list.Begin(rect);
            list.Label("Renown: witnessed deeds, sourced stories and local influence.");
            list.CheckboxLabeled("Enable witnessed personal-wealth prestige",ref Settings.WealthPrestige,"Off by default. Observers can notice at least2000 market value in worn/equipped items, plus a solely assigned bedroom only while both pawns are inside. No inventory or shared stockpile credit. Thirty-day per-pawn observation cooldown; possessions are not proof of merit. Up to eight colonists checked per game hour.");
            list.CheckboxLabeled("Enable warned caravan threats",ref Settings.CaravanThreats,"Off by default. A fresh hostile-faction report and Fame60 after day14 can trigger a two-day warning for one adult caravan member. Shares the45-day warning budget with home attacks and bounty hunts. Returning to a map, peace, lost evidence or disabling cancels pending dispatch. Native100..350-point encounters target the named pawn; combat can injure or kill, including during retreat. Attackers retreat after18,000 ticks or target loss. No automatic surrender or kidnapping. Disabling does not undo an encounter already queued or arrived.");
            list.CheckboxLabeled("Enable warned bounty hunts",ref Settings.BountyHunts,"Off by default. Sourced hostile-faction reports and Fame60 after day14 can prompt an enemy capture offer of1200..2000 silver. Two-day warning; moving the target away, ending hostility, losing evidence or disabling cancels pending dispatch. Shares one45-day warning budget with targeted attacks; if both are enabled, eligible warning kinds alternate. Hunters use native combat and may carry only the named downed pawn away. The enemy offer is not a player payment or guaranteed capture. Hunting lasts at most18,000 ticks; remaining hunters then retreat and release anyone still being carried. Disabling cancels pending warnings, not hunters already dispatched.");
            list.CheckboxLabeled("Enable warned targeted attacks",ref Settings.TargetedAttacks,"Off by default. From day14, Fame60 and a fresh hostile faction report can cause a two-day warning. One warning per45 days. Moving the subject away, ending hostility, losing qualifying evidence or disabling cancels pending dispatch. Existing threats block dispatch. Native edge attackers receive100..350 requested points and retreat after18,000 ticks or losing the target. Disabling cancels pending warnings; already dispatched groups retain their retreat limit. No theft or kidnapping in this option.");
            list.CheckboxLabeled("Enable warned Crew splintering",ref Settings.CrewSplintering,"Off by default; requires protests and a future notice. Crews of six or more with three members below50 opinion of their leader can face a separate two-day warning. If unresolved, dissenters and supporters receive mutual -12 opinion for five days. Crew determines regrouping. Restore needs to60%, raise any dissenter's leader opinion to50, change membership or disable either option to cancel before effect. One warning per thirty days. No exile or faction change; existing memories expire naturally.");
            list.CheckboxLabeled("Enable warned Crew sabotage",ref Settings.CrewSabotage,"Off by default; requires protests and a future protest notice. If unresolved, a separate one-day warning may allow one repairable electric tailoring/smithing/machining bench breakdown. Resolve needs, change the crew, remove the target or disable either option to cancel. One warning per thirty days; no fire or essential systems.");
            list.CheckboxLabeled("Enable sourced Crew persuasion",ref Settings.CrewPersuasion,"Off by default. Crew outreach and rivalry accounts can add +4 or -4 opinion for three days based on the listener's own evidence appraisal, once per subject per five days. Crew alone recalculates membership. Disabling stops new persuasion; existing memories expire normally.");
            list.CheckboxLabeled("Enable reputation-driven pilgrims",ref Settings.Pilgrims,"Off by default. Visitors seek one sourced account about a renowned pawn. Shares a thirty-day visitor budget with reporters. With recruitment enabled, a favourable adult visitor may ask to stay after one day: accept/decline, one-day expiry, shared thirty-day recruitment budget. No automatic joining or loyalty guarantee; hosting is separate.");
            list.CheckboxLabeled("Enable reputation-driven reporters",ref Settings.Reporters,"Off by default. Visitors collect up to three sourced accounts, one per game hour; shared thirty-day visitor budget. With Hosting enabled, a returning party can publish observed hosting conditions: at most +1 or -1 goodwill to other eligible non-hostile factions, no reduction below zero, once per thirty days. Their own faction's hosting change is separate, not charged twice. No invented pawn accounts or inferred blame.");
            list.CheckboxLabeled("Enable reputation-backed mercenary contracts",ref Settings.MercenaryContracts,"Off by default. After day seven, Fame50 and fresh faction reports can attract a native bandit-camp quest. One attempt per thirty days globally, one outstanding offer/contract. Review danger, travel, rewards and deadlines in Quests; nothing is accepted automatically. Disabling stops new offers, not existing quests. No paid DLC required.");
            list.CheckboxLabeled("Enable envoy military-aid requests",ref Settings.MilitaryAid,"Off by default. A Fame30 informed envoy at an allied industrial settlement can request native aid for a chosen home map. Native goodwill cost, shared one-day cooldown, temperature and enemy warnings apply. Request submission does not guarantee arrival or a refund. Independent of goodwill-visit and trade-discount options.");
            list.CheckboxLabeled("Enable earned emissary trade discounts",ref Settings.EmissaryTrading,"Off by default; requires emissary visits. New successful visits earn up to 5% off goods bought at the visited settlement by that envoy for two days. No pawn/silver discount or selling-price increase; native floors and a selling-price floor apply. Old visits grant nothing retroactively. Disabling stops the benefit without extending its expiry.");
            list.CheckboxLabeled("Enable player-led emissary visits",ref Settings.Emissaries,"Off by default. At a non-hostile settlement, select a renowned caravan member and confirm a visit. Requires Fame30 and fresh evidence already held by that faction. Chance uses fame, Social and the leader's personally informed influence. Up to +3 native goodwill or no change; twenty-day cooldown per faction includes unsuccessful visits. Trade discounts and military-aid requests have separate options. No automatic travel.");
            list.CheckboxLabeled("Enable reputation-backed trade contracts",ref Settings.Contracts,"Off by default. After day seven, Fame50 and fresh reports can attract an optional native trade request from that faction's nearby reachable settlement. One attempt per thirty days globally, including failures and ignored offers. Review goods, rewards and deadlines in Quests. Native fulfilment includes +12 goodwill. Disable to stop new offers, not to cancel existing quests.");
            list.CheckboxLabeled("Enable delegation hosting reports",ref Settings.Hosting,"Off by default. Tracks only new Renown delegations arriving while enabled. Six-hour grace for food, exhausted unsheltered guests and injury/captivity concerns. A returning member can report +1 goodwill or at most -2 after sustained concerns; no Renown reduction below0. Native consequences still apply. Disabling closes tracking without reward or penalty.");
            list.CheckboxLabeled("Enable reputation-driven recruitment offers",ref Settings.Recruits,"Off by default. After day seven, fresh faction reports and Fame50 can trigger a native wanderer accept/reject letter. One attempt per30 days globally, including failures and declined offers. Native quest chooses an eligible home map. No skill/loyalty guarantee; additional to storyteller recruitment. Disable to stop future attempts.");
            list.CheckboxLabeled("Enable reputation-driven visiting delegations",ref Settings.Delegations,"Off by default. After day seven, a non-hostile faction with a fresh report about a locally renowned pawn can send native peaceful visitors. At most one attempt per twenty days globally, including failures. Independent of traders. Hosting reports are a separate opt-in; no forced joining or treaties.");
            list.CheckboxLabeled("Enable Crew protests",ref Settings.CrewProtests,"Off by default. A warned unmet request may receive a separate one-day notice, then at most one day of -3 mood instead of its -2 unmet memory. Improving the need or disabling this option ends it. No jobs are interrupted.");
            list.CheckboxLabeled("Enable limited production strikes",ref Settings.CrewStrikes,"Off by default; requires Crew protests. Only future notices include a strike warning. During the one-day protest, members may skip new automatic sculpture, apparel and smithing jobs. Current jobs, emergency work and prioritized orders are untouched. Disabling either option immediately restores eligibility.");
            list.CheckboxLabeled("Enable experimental reputation-driven trader visits",ref Settings.TraderVisits,
                "Off by default. Requires known deeds carried to a non-hostile trading faction, local fame of at least 30, and native arrival checks. At most one attempt per ten game days across all maps, after day three. No raids or free goods.");
            list.CheckboxLabeled("Enable experimental reputation diplomacy",ref Settings.Diplomacy,
                "A renowned colonist may make a diplomatic introduction to a non-hostile visitor who knows them, after their faction received a sourced report. At most +2 goodwill per five days globally. No paid DLC or automatic alliances.");
            list.Label("Recognition, stories and Crew recreation requests do not require these options. Optional events use native jobs and incidents; ordinary raids are not replaced.");list.End();
        }
    }
    public partial class GameComponent_Renown
    {
        int lastVisitAttempt=-1,lastVisitSuccess=-1,lastDiplomacy=-1;
        public int LastVisitAttempt=>lastVisitAttempt;
        public bool VisitEligible(FactionKnowledge account,Map map)
        {
            return account!=null && map!=null && map.IsPlayerHome && Person(account.Maker) && account.Maker.IsFreeColonist && account.Maker.Map==map &&
                Fame(account.Maker)>=30 && account.Faction!=null && !account.Faction.IsPlayer && !account.Faction.defeated &&
                !account.Faction.HostileTo(Faction.OfPlayer) && !account.Faction.def.caravanTraderKinds.NullOrEmpty() &&
                Policy.Fresh(account.Tick,Find.TickManager.TicksGame) && Deeds.Any(d=>d.Id==account.DeedId);
        }
        public bool TryVisit(Map map)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.TraderVisits!=true || now<3*Policy.Day || lastVisitAttempt>=0 && now-lastVisitAttempt<10*Policy.Day)return false;
            var account=Abroad.FirstOrDefault(a=>VisitEligible(a,map));if(account==null)return false;
            lastVisitAttempt=now; // Failed native checks also consume the global attempt budget.
            var incident=DefDatabase<IncidentDef>.GetNamedSilentFail("TraderCaravanArrival");if(incident==null)return false;
            var parms=StorytellerUtility.DefaultParmsNow(incident.category,map);parms.faction=account.Faction;parms.forced=false;
            if(!incident.Worker.CanFireNow(parms) || !incident.Worker.TryExecute(parms))return false;
            lastVisitSuccess=now;
            DisplayBridge.Record("News of "+account.Maker.LabelShort+" helped attract traders from "+account.Faction.Name+".",
                "Optional reputation visit: a departing carrier established faction awareness. Native incident eligibility and arrival generation were used. Global ten-day attempt cooldown.",account.Maker,null,map);
            return true;
        }
        void RefreshVisits()
        {
            if(RenownMod.Settings?.TraderVisits!=true)return;
            foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryVisit(map))break;
        }
        public bool CanDiplomacy(Pawn speaker,Pawn visitor)
        {
            int now=Find.TickManager.TicksGame;
            return RenownMod.Settings?.Diplomacy==true && Conversation(speaker,visitor) && speaker.IsFreeColonist &&
                visitor.Faction!=null && !visitor.Faction.IsPlayer && !visitor.IsPrisoner && !visitor.Faction.HostileTo(Faction.OfPlayer) &&
                Fame(speaker)>=30 && Knows(visitor,speaker)!=null && (visitor.relations?.OpinionOf(speaker)??-100)>=0 &&
                !Accounts(visitor,speaker).Any(k=>Evidence(k)?.Kind=="execution" && Evidence(k).SubjectFaction==visitor.Faction) &&
                Abroad.Any(a=>a.Faction==visitor.Faction && a.Maker==speaker && Policy.Fresh(a.Tick,now) && Deeds.Any(d=>d.Id==a.DeedId)) &&
                (lastDiplomacy<0 || now-lastDiplomacy>=5*Policy.Day);
        }
        public bool Diplomacy(Pawn speaker,Pawn visitor)
        {
            if(!CanDiplomacy(speaker,visitor))return false;
            lastDiplomacy=Find.TickManager.TicksGame;
            bool changed=Faction.OfPlayer.TryAffectGoodwillWith(visitor.Faction,2,false,false);
            DisplayBridge.Record(speaker.LabelShort+" made a diplomatic introduction to "+visitor.LabelShort+".",
                "The visitor and their faction held sourced knowledge. Native goodwill adjustment requested: +2, subject to native limits. Global five-day cooldown. No automatic alliance or treaty.",speaker,visitor,speaker.Map);
            return changed;
        }
        void ExposeVisits()
        {Scribe_Values.Look(ref lastVisitAttempt,"renownLastVisitAttempt",-1);Scribe_Values.Look(ref lastVisitSuccess,"renownLastVisitSuccess",-1);Scribe_Values.Look(ref lastDiplomacy,"renownLastDiplomacy",-1);}
    }
    public class InteractionWorker_Diplomacy : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.CanDiplomacy(initiator,recipient)==true?.2f:0;
        public override void Interacted(Pawn initiator,Pawn recipient,System.Collections.Generic.List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Diplomacy(initiator,recipient);}
    }
}
