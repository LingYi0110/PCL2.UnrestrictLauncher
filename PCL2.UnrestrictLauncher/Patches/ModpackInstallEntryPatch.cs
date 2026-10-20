using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher.Patches
{
    [HarmonyPatch("PCL.ModModpack", "ModpackInstall", MethodType.Normal)]
    [HarmonyPatch(new Type[0])]
    internal static class ModpackInstallEntryPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            if (code.Any(instruction => instruction.Calls(AccessTools.Method("MeloongCore.Wpf.Dialogs:SelectFile"))))
                return code;
            if (code.Count < 3 || code[0].opcode != OpCodes.Newobj || !code[1].IsStloc()
                || !code.Any(instruction => instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string && ((string)instruction.operand).Contains("PCL 暂时不再提供 MC 下载功能"))
                || code.Any(instruction => instruction.blocks.Count != 0))
                throw new InvalidOperationException("Unsupported manual modpack installation entry IL.");
            var closure = ((ConstructorInfo)code[0].operand).DeclaringType;
            if (AccessTools.Field(closure, "$VB$Local_File")?.FieldType != typeof(string)
                || AccessTools.Method(closure, "_Lambda$__0", Type.EmptyTypes)?.ReturnType != typeof(void))
                throw new MissingMemberException("The original manual modpack installation callback is unavailable.");
            return new[]
            {
                code[0], code[1], CodeInstruction.LoadLocal(code[1].LocalIndex()),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModpackInstallEntryPatch), nameof(SelectAndInstall))),
                new CodeInstruction(OpCodes.Ret)
            };
        }

        private static void SelectAndInstall(object callback)
        {
            try
            {
                var selectFile = AccessTools.Method("MeloongCore.Wpf.Dialogs:SelectFile");
                var filterType = Nullable.GetUnderlyingType(selectFile.GetParameters()[3].ParameterType);
                var filter = AccessTools.Method(filterType, "op_Implicit", new[] { typeof(string) })
                    .Invoke(null, new object[] { "整合包文件(*.rar;*.zip;*.mrpack)|*.rar;*.zip;*.mrpack" });
                var files = (IEnumerable<string>)selectFile.Invoke(null,
                    new object[] { "选择整合包压缩文件", false, null, filter });
                var file = files.FirstOrDefault();
                if (string.IsNullOrEmpty(file)) return;
                AccessTools.Field(callback.GetType(), "$VB$Local_File").SetValue(callback, file);
                var action = (Action)Delegate.CreateDelegate(typeof(Action), callback,
                    AccessTools.Method(callback.GetType(), "_Lambda$__0", Type.EmptyTypes));
                // Keep PCL's original cancellation handling and error log in its callback.
                AccessTools.Method("PCL.ModBase:RunInThread", new[] { typeof(Action) })
                    .Invoke(null, new object[] { action });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
