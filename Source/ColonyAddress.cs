using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        Dictionary<Pawn,int> addressAttempts=new Dictionary<Pawn,int>();
        List<Pawn> addressKeys;
        List<int> addressValues;
        public static bool AddressSpeakerEligible(Pawn pawn)=>Attentive(pawn) && pawn.IsFreeColonist && !pawn.Drafted && pawn.Map.IsPlayerHome && !pawn.WorkTagIsDisabled(WorkTags.Social);
        public bool CanAddress(Pawn pawn)=>AddressSpeakerEligible(pawn) &&
            (!addressAttempts.TryGetValue(pawn,out int last) || Find.TickManager.TicksGame-last>=7*Policy.Day) &&
            addressAttempts.Count(p=>p.Key!=null && !p.Key.Dead && Find.TickManager.TicksGame-p.Value<7*Policy.Day)<256;
        void PruneAddresses()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var pawn in addressAttempts.Where(p=>p.Key.Dead || now-p.Value>=7*Policy.Day).Select(p=>p.Key).ToArray())addressAttempts.Remove(pawn);
        }
        public bool StartAddress(Pawn pawn)
        {
            if(!CanAddress(pawn))return false;
            PruneAddresses();
            var job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RN_ColonyAddress"));
            if(!pawn.jobs.TryTakeOrderedJob(job))return false;
            addressAttempts[pawn]=Find.TickManager.TicksGame;return true;
        }
        public bool CompleteAddress(Pawn pawn,IEnumerable<Pawn> audience)
        {
            if(!(pawn?.jobs?.curDriver is JobDriver_ColonyAddress driver) || !AddressSpeakerEligible(pawn) || !driver.ClaimCompletion())return false;
            var witnesses=audience.Where(p=>Sees(p,pawn) && !p.HostileTo(pawn)).Distinct().Take(256).ToList();
            if(witnesses.Count==0)return false;
            int now=Find.TickManager.TicksGame;
            var deed=new Deed{Id=nextId++,Kind="speech",Maker=pawn,Map=pawn.Map,Tick=now,ProductId="speech:"+pawn.ThingID+":"+now,ProductLabel="a colony address",Witnesses=witnesses};
            Deeds.Add(deed);
            foreach(var observer in witnesses)
            {
                var prior=Known.FirstOrDefault(k=>k.Witness==observer && k.Maker==pawn);
                if(prior==null)Known.Add(new Knowledge{Witness=observer,Maker=pawn,DeedId=deed.Id,LearnedAt=now});
                else{Archive(prior);prior.DeedId=deed.Id;prior.Source=null;prior.Hops=0;prior.LearnedAt=now;}
                DisplayBridge.Record(observer.LabelShort+" heard "+pawn.LabelShort+" give a colony address.","Present and attentive for at least half of the completed address, and present at its conclusion. Recognition is not agreement. No ideology conversion or forced loyalty.",observer,pawn,pawn.Map);
            }
            Prune();return true;
        }
        void ExposeAddresses()
        {
            Scribe_Collections.Look(ref addressAttempts,"renownAddressAttempts",LookMode.Reference,LookMode.Value,ref addressKeys,ref addressValues);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){addressAttempts=addressAttempts??new Dictionary<Pawn,int>();PruneAddresses();}
        }
    }
    public class JobDriver_ColonyAddress:JobDriver
    {
        int elapsed;
        bool finishing,recorded;
        Dictionary<Pawn,int> attendance=new Dictionary<Pawn,int>();
        List<Pawn> attendanceKeys;
        List<int> attendanceValues;
        public int Elapsed=>elapsed;
        internal bool ClaimCompletion(){if(!finishing || recorded)return false;recorded=true;return true;}
        public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
        public override void ExposeData()
        {
            base.ExposeData();Scribe_Values.Look(ref elapsed,"renownAddressElapsed");
            Scribe_Values.Look(ref recorded,"renownAddressRecorded");
            Scribe_Collections.Look(ref attendance,"renownAddressAttendance",LookMode.Reference,LookMode.Value,ref attendanceKeys,ref attendanceValues);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)attendance=attendance??new Dictionary<Pawn,int>();
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(()=>!GameComponent_Renown.AddressSpeakerEligible(pawn));
            var speaking=Toils_General.Wait(1500);
            speaking.AddPreTickAction(()=>
            {
                elapsed++;
                if(elapsed%30!=0)return;
                foreach(var observer in pawn.Map.mapPawns.AllPawnsSpawned.Where(p=>GameComponent_Renown.Sees(p,pawn) && !p.HostileTo(pawn)).Take(256))
                {
                    if(!attendance.ContainsKey(observer) && attendance.Count>=256)continue;
                    attendance.TryGetValue(observer,out int ticks);attendance[observer]=ticks+30;
                }
            });
            yield return speaking;
            yield return Toils_General.Do(()=>{finishing=true;GameComponent_Renown.Current?.CompleteAddress(pawn,attendance.Where(p=>p.Value>=750).Select(p=>p.Key));finishing=false;});
        }
    }
    [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
    public static class AddressGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance)
        {
            foreach(var gizmo in __result)yield return gizmo;
            if(!__instance.IsFreeColonist || !__instance.Spawned)yield break;
            var command=new Command_Action{icon=TexCommand.GatherSpotActive,defaultLabel="Colony address",defaultDesc="Speak here for 1,500 ticks. Nearby attentive listeners must hear at least half and remain for the conclusion. No forced attendance or agreement. One attempt per speaker every seven days, even if interrupted. No DLC required.",action=()=>GameComponent_Renown.Current?.StartAddress(__instance)};
            if(GameComponent_Renown.Current?.CanAddress(__instance)!=true)command.Disable("Requires an awake, capable, undrafted social-capable colonist at home, no address attempt in the last seven days, and fewer than 256 active speaker cooldowns.");
            yield return command;
        }
    }
}
