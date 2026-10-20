using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PCL2.UnrestrictLauncher
{
    internal static class Bootstrap
    {
        private static Assembly _harmony;

        [STAThread]
        private static int Main(string[] args)
        {
            using (var resource = typeof(Bootstrap).Assembly.GetManifestResourceStream(
                "PCL2.UnrestrictLauncher.0Harmony.dll"))
            {
                if (resource == null)
                    throw new FileNotFoundException("The embedded Harmony assembly is missing.");
                using (var buffer = new MemoryStream())
                {
                    resource.CopyTo(buffer);
                    _harmony = Assembly.Load(buffer.ToArray());
                }
            }
            AppDomain.CurrentDomain.AssemblyResolve += ResolveHarmony;
            return Run(args);
        }

        private static Assembly ResolveHarmony(object sender, ResolveEventArgs args)
        {
            return string.Equals(new AssemblyName(args.Name).Name, "0Harmony", StringComparison.OrdinalIgnoreCase)
                ? _harmony : null;
        }

        // Resolve Harmony before the JIT compiles any code that references its types.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            return Program.Main(args);
        }
    }
}
