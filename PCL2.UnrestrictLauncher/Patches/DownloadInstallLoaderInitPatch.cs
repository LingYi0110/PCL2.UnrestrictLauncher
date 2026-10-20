using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher.Patches
{
    [HarmonyPatch("PCL.PageDownloadInstall", "LoaderInit", MethodType.Normal)]
    [HarmonyPatch(new Type[0])]
    internal static class DownloadInstallLoaderInitPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var loader = AccessTools.Field("PCL.ModDownload:DlClientListLoader");
            if (loader == null) throw new MissingFieldException("Missing DlClientListLoader.");
            if (code.Any(instruction => instruction.LoadsField(loader))) return code;

            var starts = Enumerable.Range(0, code.Count)
                .Where(i => code[i].opcode == OpCodes.Ldstr && Equals(code[i].operand, "关闭下载")).ToArray();
            if (starts.Length != 1)
                throw new InvalidOperationException("Cannot locate the disabled download loader uniquely.");
            int start = starts[0];
            int end = code.FindIndex(start, instruction => instruction.opcode == OpCodes.Newobj
                && ((ConstructorInfo)instruction.operand).DeclaringType == loader.FieldType);
            if (end < 0 || code.Skip(start).Take(end - start + 1).Any(instruction => instruction.blocks.Count != 0))
                throw new InvalidOperationException("Unexpected disabled download loader IL structure.");

            var replacement = new CodeInstruction(OpCodes.Ldsfld, loader).MoveLabelsFrom(code[start]);
            // The removed labels belong to the removed delegate-cache branches.
            var removedLabels = new HashSet<Label>(code.Skip(start + 1).Take(end - start).SelectMany(instruction => instruction.labels));
            if (code.Take(start).Concat(code.Skip(end + 1)).Any(instruction =>
                instruction.operand is Label && removedLabels.Contains((Label)instruction.operand)
                || instruction.operand is Label[] && ((Label[])instruction.operand).Any(removedLabels.Contains)))
                throw new InvalidOperationException("Unexpected branch into the disabled download loader.");
            code.RemoveRange(start, end - start + 1);
            code.Insert(start, replacement);
            return code;
        }
    }
}
