using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace Renown
{
    [HarmonyPatch(typeof(PawnGenerator),"TryGenerateNewPawnInternal")]
    public static class RejectedPawnCleanup
    {
        public static bool Applied { get; private set; }
        public static bool TryInsertCleanup(List<CodeInstruction> source,out List<CodeInstruction> result)
        {
            result=source;
            var matches=source.Select((c,i)=>new{c,i}).Where(x=>x.c.opcode==OpCodes.Ldstr && (string)x.c.operand=="Generated pawn incapable of required skill: ").Select(x=>x.i).ToArray();
            if(matches.Length!=1)return false;
            int marker=matches[0],start=marker-1;
            if(start<1 || source[start].opcode!=OpCodes.Ldarg_1 || source[start].blocks.Count!=0)return false;
            int field=-1;
            for(int i=start-1;i>=System.Math.Max(1,start-20);i--)
                if(source[i].opcode==OpCodes.Ldfld && source[i].operand is FieldInfo f && f.FieldType==typeof(Pawn)){field=i;break;}
            if(field<1)return false;
            var load=source[field-1];
            if(load.opcode!=OpCodes.Ldloc_0 && load.opcode!=OpCodes.Ldloc_1 && load.opcode!=OpCodes.Ldloc_2 && load.opcode!=OpCodes.Ldloc_3 && load.opcode!=OpCodes.Ldloc && load.opcode!=OpCodes.Ldloc_S)return false;
            var cleanup=AccessTools.Method(typeof(PawnGenerator),"DiscardGeneratedPawn",new[]{typeof(Pawn)});
            int end=source.FindIndex(marker,c=>c.opcode==OpCodes.Leave || c.opcode==OpCodes.Leave_S);
            if(cleanup==null || end<0 || end-marker>20 || !source.Skip(marker).Take(end-marker).Any(c=>c.opcode==OpCodes.Stind_Ref) || !source.Skip(marker).Take(end-marker).Any(c=>c.opcode==OpCodes.Ldnull) || source.Skip(field).Take(end-field).Any(c=>c.Calls(cleanup)))return false;
            result=source.Select(c=>new CodeInstruction(c)).ToList();
            var first=new CodeInstruction(load.opcode,load.operand);first.labels.AddRange(result[start].labels);result[start].labels.Clear();
            result.InsertRange(start,new[]{first,new CodeInstruction(OpCodes.Ldfld,source[field].operand),new CodeInstruction(OpCodes.Call,cleanup)});
            return true;
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source=instructions.ToList();Applied=TryInsertCleanup(source,out var result);
            if(!Applied)Log.Warning("[Renown] Required-skill rejection safeguard unavailable: native instruction layout differs.");
            return result;
        }
    }
}
