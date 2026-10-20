using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher
{
    [HarmonyPatch]
    internal static class RuntimeFixes
    {
        internal static Assembly EntryAssembly { get; set; }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Assembly> ByteCache = new Dictionary<string, Assembly>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Assembly), nameof(Assembly.GetEntryAssembly))]
        private static bool GetEntryAssembly(ref Assembly __result)
        {
            if (EntryAssembly == null) return true;
            __result = EntryAssembly;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Assembly), nameof(Assembly.Load), new[] { typeof(string) })]
        private static bool LoadByName(string assemblyString, ref Assembly __result)
        {
            AssemblyName name;
            try { name = new AssemblyName(assemblyString); }
            catch { return true; }

            __result = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                string.Equals(assembly.FullName, name.FullName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
            return __result == null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Assembly), nameof(Assembly.Load), new[] { typeof(byte[]) })]
        private static bool LoadBytes(byte[] rawAssembly, ref Assembly __result)
        {
            if (rawAssembly == null || rawAssembly.Length == 0) return true;
            var hash = Hash(rawAssembly);
            lock (Sync)
                return !ByteCache.TryGetValue(hash, out __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Assembly), nameof(Assembly.Load), new[] { typeof(byte[]) })]
        private static void RememberBytes(byte[] rawAssembly, Assembly __result)
        {
            if (__result != null && rawAssembly != null && rawAssembly.Length > 0)
                Remember(rawAssembly, __result);
        }

        // Preloaded dependencies must also be cached before PatchAll installs Load(byte[]).
        internal static void Remember(byte[] bytes, Assembly assembly)
        {
            var hash = Hash(bytes);
            lock (Sync) ByteCache[hash] = assembly;
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}
