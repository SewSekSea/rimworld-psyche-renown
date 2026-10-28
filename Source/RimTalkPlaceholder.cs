using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Renown
{
    [HarmonyPatch]
    public static class RimTalkPlaceholder
    {
        public static bool Applied { get; private set; }
        static MethodBase TargetMethod()=>AccessTools.Method(AccessTools.TypeByName("RimTalk.Data.Cache"),"InitializePlayerPawn",System.Type.EmptyTypes);
        static bool Prepare()=>AccessTools.TypeByName("RimTalk.Data.Cache")!=null && TargetMethod()!=null;
        public static bool TryRewrite(List<CodeInstruction> source,out List<CodeInstruction> result)
        {
            result=source;
            var original=AccessTools.Method(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnKindDef),typeof(Faction),typeof(PlanetTile?)});
            var matches=source.Select((c,i)=>new{c,i}).Where(x=>x.c.Calls(original)).Select(x=>x.i).ToArray();
            if(matches.Length!=1)return false;
            int index=matches[0];
            if(index+1>=source.Count || source[index+1].opcode!=OpCodes.Stsfld || !(source[index+1].operand is FieldInfo field) || field.Name!="_playerPawn" || field.FieldType!=typeof(Pawn) || field.DeclaringType?.FullName!="RimTalk.Data.Cache")return false;
            result=source.Select(c=>new CodeInstruction(c)).ToList();
            result[index].opcode=OpCodes.Call;
            result[index].operand=AccessTools.Method(typeof(RimTalkPlaceholder),nameof(Generate));
            return true;
        }
        static Pawn Generate(PawnKindDef kind,Faction faction,PlanetTile? tile)=>PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,faction,tile:tile??PlanetTile.Invalid,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Applied=TryRewrite(instructions.ToList(),out var result);
            if(!Applied)Log.Warning("[Renown] RimTalk placeholder safeguard unavailable: instruction layout differs.");
            return result;
        }
    }
}
