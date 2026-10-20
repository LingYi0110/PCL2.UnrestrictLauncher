using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using Expression = System.Linq.Expressions.Expression;

namespace PCL2.UnrestrictLauncher.Patches
{
    [HarmonyPatch("PCL.PageDownloadResourceDetail", "InitializeStackResourceVersions", MethodType.Normal)]
    internal static class CompDetailVersionListPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var toListItem = AccessTools.Method("PCL.ResourceVersion:ToListItem");
            if (toListItem == null || toListItem.GetParameters().Length != 3 || toListItem.ReturnType.IsValueType)
                throw new MissingMethodException("Unsupported ResourceVersion.ToListItem signature.");
            // A restored upstream method already supplies the install handler.
            if (code.Any(instruction => instruction.opcode == OpCodes.Ldftn
                && instruction.operand is MethodInfo && ((MethodInfo)instruction.operand).Name == "Install_Click"))
                return code;
            var matches = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(toListItem)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Cannot locate the resource version item call uniquely.");
            int index = matches[0];
            var original = code[index];
            code.RemoveAt(index);
            code.InsertRange(index, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(original).MoveBlocksFrom(original),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompDetailVersionListPatch), nameof(CreateItem))),
                new CodeInstruction(OpCodes.Castclass, toListItem.ReturnType)
            });
            return code;
        }

        private static object CreateItem(object version, Delegate primary, Delegate secondary, bool badDisplay, object page)
        {
            var method = AccessTools.Method(version.GetType(), "ToListItem");
            if (Convert.ToInt32(AccessTools.Field(page.GetType(), "TargetResourceType").GetValue(page)) == 2)
            {
                var parameters = method.GetParameters();
                primary = Bind(page, "Install_Click", parameters[0].ParameterType);
                secondary = Bind(page, "Save_Click", parameters[1].ParameterType);
            }
            try { return method.Invoke(version, new object[] { primary, secondary, badDisplay }); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static Delegate Bind(object page, string methodName, Type delegateType)
        {
            var method = AccessTools.Method(page.GetType(), methodName);
            var parameters = AccessTools.Method(delegateType, "Invoke").GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
            var targetParameters = method.GetParameters();
            var arguments = parameters.Select((parameter, i) =>
                Expression.Convert(parameter, targetParameters[i].ParameterType));
            return Expression.Lambda(delegateType,
                Expression.Call(Expression.Constant(page), method, arguments), parameters).Compile();
        }
    }
}
