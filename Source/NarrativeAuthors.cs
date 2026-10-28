using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    [HarmonyPatch(typeof(Book),nameof(Book.GenerateBook))]
    public static class BookAuthorScope
    {
        [ThreadStatic] static int depth;
        public static bool Active=>depth>0;
        static void Prefix(out int __state){__state=depth;depth++;}
        static void Finalizer(int __state){depth=__state;}
    }
    [HarmonyPatch(typeof(TaleData_Pawn),nameof(TaleData_Pawn.GenerateRandom))]
    public static class NarrativeAuthors
    {
        [ThreadStatic] static int depth;
        public static bool Active=>depth>0;
        static void Prefix(out int __state){__state=depth;if(BookAuthorScope.Active)depth++;}
        static void Finalizer(int __state){depth=__state;}
    }
    [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
    public static class NarrativeAuthorRequest
    {
        static void Prefix(ref PawnGenerationRequest request)
        {
            if(!NarrativeAuthors.Active)return;
            request.CanGeneratePawnRelations=false;
            request.ForceGenerateNewPawn=true;
        }
    }
}
