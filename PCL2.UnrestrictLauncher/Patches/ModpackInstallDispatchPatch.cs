using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher.Patches
{
    [HarmonyPatch("PCL.ModModpack", "ModpackInstall", MethodType.Normal)]
    [HarmonyPatch(new[] { typeof(string), typeof(string), typeof(string) })]
    internal static class ModpackInstallDispatchPatch
    {
        internal const string Source = @"E:\Projects\PCL\本体代码\Plain Craft Launcher 2\Modules\Minecraft\ModModpack.vb";

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase __originalMethod)
        {
            var code = instructions.ToList();
            if (code.Any(instruction => instruction.operand is MethodInfo
                && ((MethodInfo)instruction.operand).Name.StartsWith("InstallPack", StringComparison.Ordinal)))
                return code;
            var matches = Enumerable.Range(0, Math.Max(0, code.Count - 2)).Where(i =>
                code[i].opcode == OpCodes.Ldstr && code[i].operand is string
                && ((string)code[i].operand).Contains("PCL 暂时不再提供 MC 下载功能")
                && code[i + 1].opcode == OpCodes.Newobj
                && Equals(code[i + 1].operand, AccessTools.Constructor(typeof(Exception), new[] { typeof(string) }))
                && code[i + 2].opcode == OpCodes.Throw).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Cannot locate the disabled modpack dispatch uniquely.");
            int index = matches[0];
            var locals = __originalMethod.GetMethodBody().LocalVariables;
            var archive = locals.Where(local => local.LocalType == typeof(ZipArchive)).ToArray();
            var packType = locals.Where(local => local.LocalType == typeof(int)).ToArray();
            var baseStores = Enumerable.Range(0, code.Count - 1).Where(i =>
                code[i].opcode == OpCodes.Ldstr && Equals(code[i].operand, "") && code[i + 1].IsStloc()).ToArray();
            var returnType = ((MethodInfo)__originalMethod).ReturnType;
            if (archive.Length != 1 || packType.Length != 1 || baseStores.Length != 1
                || locals[code[baseStores[0] + 1].LocalIndex()].LocalType != typeof(string)
                || returnType == typeof(void) || returnType.IsValueType
                || !code.Skip(index + 3).Any(instruction => instruction.blocks.Any(block => block.blockType == ExceptionBlockType.BeginFinallyBlock))
                || code.Last().opcode != OpCodes.Endfinally)
                throw new InvalidOperationException("Unsupported modpack dispatch locals or finally structure.");

            var result = generator.DeclareLocal(returnType);
            var done = generator.DefineLabel();
            var replacement = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                CodeInstruction.LoadLocal(archive[0].LocalIndex),
                CodeInstruction.LoadLocal(code[baseStores[0] + 1].LocalIndex()),
                CodeInstruction.LoadLocal(packType[0].LocalIndex),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModpackInstallDispatchPatch), nameof(Dispatch))),
                new CodeInstruction(OpCodes.Castclass, returnType),
                new CodeInstruction(OpCodes.Stloc, result),
                // Leave the try normally, so PCL's original finally still disposes the archive.
                new CodeInstruction(OpCodes.Leave, done)
            };
            foreach (var instruction in code.Skip(index).Take(3))
            {
                replacement[0].labels.AddRange(instruction.labels);
                foreach (var block in instruction.blocks)
                    (block.blockType == ExceptionBlockType.EndExceptionBlock
                        ? replacement[replacement.Count - 1] : replacement[0]).blocks.Add(block);
            }
            code.RemoveRange(index, 3);
            code.InsertRange(index, replacement);
            var loadResult = new CodeInstruction(OpCodes.Ldloc, result);
            loadResult.labels.Add(done);
            code.Add(loadResult);
            code.Add(new CodeInstruction(OpCodes.Ret));
            return code;
        }

        private static object Dispatch(string file, string instanceName, string logo,
            ZipArchive archive, string baseFolder, int packType)
        {
            string name;
            string display;
            switch (packType)
            {
                case 0: name = "InstallPackCurseForge"; display = "CurseForge"; break;
                case 1: name = "InstallPackHMCL"; display = "HMCL"; break;
                case 2: name = "InstallPackMMC"; display = "MMC"; break;
                case 3: name = "InstallPackMCBBS"; display = "MCBBS"; break;
                case 4: name = "InstallPackModrinth"; display = "Modrinth"; break;
                case 9: name = "InstallPackLauncherPack"; display = "带启动器的压缩包"; break;
                default: name = "InstallPackCompress"; display = "未能识别，假定为压缩包"; break;
            }
            PclLog.Info("整合包种类：" + display, Source);
            var install = AccessTools.Method("PCL.ModModpack:" + name);
            if (install == null)
                throw new MissingMethodException("The target application is missing a modpack installer method: " + name);
            var values = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "FileAddress", file }, { "Archive", archive }, { "ArchiveBaseFolder", baseFolder },
                { "InstanceName", instanceName }, { "Logo", logo }
            };
            var arguments = install.GetParameters().Select(parameter =>
            {
                object value;
                if (values.TryGetValue(parameter.Name, out value)) return value;
                if (parameter.HasDefaultValue) return parameter.DefaultValue;
                throw new MissingMethodException("Unknown modpack installer parameter: " + parameter.Name);
            }).ToArray();
            try { return install.Invoke(null, arguments); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
