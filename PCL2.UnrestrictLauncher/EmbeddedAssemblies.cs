using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace PCL2.UnrestrictLauncher
{
    internal static class EmbeddedAssemblies
    {
        private static readonly string[] ResourceNames =
        {
            "NAudio", "Newtonsoft.Json", "Ookii.Dialogs.Wpf", "Imazen.WebP",
            "CacheCow.Common", "CacheCow.Client.FileStore", "CacheCow.Client",
            "ThrottleDebounce", "System.Net.Http.Formatting", "Microsoft.Win32.Registry",
            "PCLCS", "MeloongCore.Wpf", "MeloongCore"
        };

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Assembly> Cache = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        private static ResourceManager _resources;
        [ThreadStatic] private static bool _resolving;

        public static void Install(Assembly target)
        {
            _resources = new ResourceManager("PCL.Resources", target);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            foreach (var name in new[] { "MeloongCore", "MeloongCore.Wpf", "PCLCS", "Newtonsoft.Json" })
            {
                if (Load(name) == null)
                    throw new FileNotFoundException("Missing embedded dependency in target application: " + name, name);
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (_resolving) return null;
            var name = new AssemblyName(args.Name).Name;
            if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
            var resourceName = ResourceNames.FirstOrDefault(resource =>
                name == resource || name.StartsWith(resource + ".", StringComparison.Ordinal));
            if (resourceName == null) return null;

            _resolving = true;
            try
            {
                return Load(resourceName);
            }
            finally
            {
                _resolving = false;
            }
        }

        private static Assembly Load(string resourceName)
        {
            lock (Sync)
            {
                Assembly assembly;
                if (Cache.TryGetValue(resourceName, out assembly)) return assembly;

                var bytes = _resources.GetObject(resourceName, CultureInfo.InvariantCulture) as byte[];
                if (bytes == null) return null;
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                    loaded => loaded.GetName().Name == resourceName) ?? Assembly.Load(bytes);
                RuntimeFixes.Remember(bytes, assembly);
                Cache[resourceName] = assembly;
                return assembly;
            }
        }
    }
}
