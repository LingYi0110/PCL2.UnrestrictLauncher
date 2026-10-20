using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Windows;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher.Patches
{
    [HarmonyPatch("PCL.PageDownloadModify", "LoadInstanceFinished", MethodType.Normal)]
    internal static class ModifyCardVisiblePatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var getter = AccessTools.PropertyGetter("PCL.PageDownloadInstall:CardMinecraft");
            var setter = AccessTools.PropertySetter(typeof(UIElement), "Visibility");
            var matches = Enumerable.Range(0, Math.Max(0, code.Count - 2)).Where(i =>
                code[i].Calls(getter) && code[i + 1].LoadsConstant((long)Visibility.Collapsed)
                && code[i + 2].Calls(setter)).ToArray();
            if (matches.Length == 0 && Enumerable.Range(0, Math.Max(0, code.Count - 2)).Any(i =>
                code[i].Calls(getter) && code[i + 1].LoadsConstant((long)Visibility.Visible)
                && code[i + 2].Calls(setter))) return code;
            if (matches.Length != 1)
                throw new InvalidOperationException("Cannot locate CardMinecraft visibility assignment uniquely.");
            var value = code[matches[0] + 1];
            value.opcode = OpCodes.Ldc_I4_0;
            value.operand = null;
            return code;
        }
    }
}
