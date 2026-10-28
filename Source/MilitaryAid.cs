using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Renown
{
    public enum AidSubmission { Unavailable, NativeConfirmationRequired, Submitted, Interrupted }
    public partial class GameComponent_Renown
    {
        static readonly MethodInfo NativeAidRequest=AccessTools.Method(typeof(FactionDialogMaker),"RequestMilitaryAidOption",new[]{typeof(Map),typeof(Faction),typeof(Pawn)});
        public DiaOption MilitaryAidOption(Pawn envoy,Caravan caravan,Settlement settlement,Map destination)
        {
            if(NativeAidRequest==null || !EmissaryEligible(envoy,caravan,settlement) || destination==null || !Find.Maps.Contains(destination) || !destination.IsPlayerHome)return null;
            var faction=settlement.Faction;
            if(!faction.def.canRequestMilitaryAid || faction.def.techLevel<TechLevel.Industrial || faction.PlayerRelationKind!=FactionRelationKind.Ally)return null;
            try{return NativeAidRequest.Invoke(null,new object[]{destination,faction,envoy}) as DiaOption;}
            catch(TargetInvocationException){return null;}
            catch(ArgumentException){return null;}
        }
        public AidSubmission SubmitMilitaryAid(Pawn envoy,Caravan caravan,Settlement settlement,Map destination,string confirmedWarning=null)
        {
            if(RenownMod.Settings?.MilitaryAid!=true)return AidSubmission.Unavailable;
            var fresh=MilitaryAidOption(envoy,caravan,settlement,destination);
            if(fresh==null || fresh.disabled)return AidSubmission.Unavailable;
            Action dispatch=fresh.action;
            if(dispatch==null)
            {
                if(fresh.link==null || confirmedWarning==null || fresh.link.text.ToString()!=confirmedWarning)return AidSubmission.NativeConfirmationRequired;
                var choices=fresh.link.options.Where(o=>!o.disabled && o.action!=null).ToList();
                if(choices.Count!=1)return AidSubmission.NativeConfirmationRequired;
                dispatch=choices[0].action;
            }
            settlement.Faction.lastMilitaryAidRequestTick=Find.TickManager.TicksGame;
            try{dispatch();return AidSubmission.Submitted;}
            catch(Exception e){Log.Warning("[Renown] Native aid request interrupted: "+e.GetType().Name);return AidSubmission.Interrupted;}
        }
        public Dialog_MessageBox MilitaryAidConfirmation(Pawn envoy,Caravan caravan,Settlement settlement,Map destination)
        {
            var option=MilitaryAidOption(envoy,caravan,settlement,destination);
            string warning=option?.action==null?option?.link?.text.ToString():null;
            int cost=settlement?.Faction==null?0:-Faction.OfPlayer.CalculateAdjustedGoodwillChange(settlement.Faction,-25);
            return Dialog_MessageBox.CreateConfirmation("Request military aid for "+destination.Parent.Label+" through "+envoy.LabelShort+"?\n\nNative goodwill cost: "+cost+". Shared faction cooldown: one day. Native strength and arrival rules apply. Submission does not guarantee arrival; a failed dispatch can still consume goodwill and cooldown. No refund or free reinforcement.\n\n"+(warning??"Eligibility is checked again when you confirm."),()=>
            {
                if(settlement?.Faction==null || -Faction.OfPlayer.CalculateAdjustedGoodwillChange(settlement.Faction,-25)!=cost){Messages.Message("Aid cost changed. Review a new request.",MessageTypeDefOf.RejectInput,false);return;}
                var result=SubmitMilitaryAid(envoy,caravan,settlement,destination,warning);
                string message=result==AidSubmission.Submitted?"Military-aid request submitted. Arrival is not guaranteed.":result==AidSubmission.NativeConfirmationRequired?"Native aid warning changed. Review a new request.":result==AidSubmission.Interrupted?"Native aid request interrupted; cooldown retained. Check goodwill before retrying.":"Military-aid request unavailable. Nothing submitted.";
                Messages.Message(message,MessageTypeDefOf.NeutralEvent,false);
            },title:"Renown military aid");
        }
        public Command_Action MilitaryAidCommand(Caravan caravan)
        {
            if(RenownMod.Settings?.MilitaryAid!=true || caravan==null || !caravan.Spawned || caravan.Faction!=Faction.OfPlayer)return null;
            var settlement=Find.WorldObjects.Settlements.FirstOrDefault(s=>s.Tile==caravan.Tile && s.Faction!=Faction.OfPlayer);
            if(settlement==null)return null;
            var command=new Command_Action{icon=TexCommand.Draft,defaultLabel="Request military aid",defaultDesc="Choose an informed Fame30 envoy and a home destination. Allied industrial faction required. Native goodwill cost and shared one-day cooldown apply. No guaranteed arrival."};
            command.action=()=>
            {
                var choices=caravan.PawnsListForReading.Where(p=>EmissaryEligible(p,caravan,settlement)).OrderBy(p=>p.thingIDNumber).Select(p=>new FloatMenuOption(p.LabelShort,()=>
                {
                    var homes=Find.Maps.Where(m=>m.IsPlayerHome).Select(m=>
                    {
                        var native=MilitaryAidOption(p,caravan,settlement,m);
                        string label=m.Parent.Label+(native==null?" (unavailable)":native.disabled?" ("+native.disabledReason+")":"");
                        return new FloatMenuOption(label,native==null || native.disabled?(Action)null:()=>Find.WindowStack.Add(MilitaryAidConfirmation(p,caravan,settlement,m)));
                    }).ToList();
                    if(homes.Count>0)Find.WindowStack.Add(new FloatMenu(homes));
                })).ToList();
                if(choices.Count>0)Find.WindowStack.Add(new FloatMenu(choices));
                else Messages.Message("No eligible informed envoy at this settlement.",MessageTypeDefOf.RejectInput,false);
            };
            return command;
        }
    }
}
