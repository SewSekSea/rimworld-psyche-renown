using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    // Observe real before/after treatment, never an intended job or predicted survival.
    [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.DoTend))]
    public static class MedicalPatch
    {
        static void Prefix(Pawn doctor,Pawn patient,out List<Hediff> __state)
        {
            List<Hediff> injuries=null;
            ObservationGuard.Run("medical capture",()=>
            {
                if(!GameComponent_Renown.Person(doctor) || !doctor.IsFreeColonist ||
                    !GameComponent_Renown.Person(patient) || doctor==patient || doctor.Map!=patient.Map ||
                    patient.HostileTo(doctor) || patient.IsPrisoner)return;
                injuries=patient.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury && h.Bleeding && h.TendableNow()).ToList();
            });
            __state=injuries;
        }
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn doctor,Pawn patient,List<Hediff> __state,bool __runOriginal)
        {
            if(!__runOriginal || __state==null || __state.Count==0)return;
            ObservationGuard.Run("medical",()=>
            {
                if(__state.Any(h=>patient.health.hediffSet.hediffs.Contains(h) &&
                    h.TryGetComp<HediffComp_TendDuration>()?.IsTended==true))
                    GameComponent_Renown.Current?.RecordDeed("medical",doctor,patient);
            });
        }
    }
}
