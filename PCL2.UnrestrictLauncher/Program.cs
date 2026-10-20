using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher
{
    public class Program
    {
        private static readonly Harmony _harmony = new Harmony("PCL2.UnrestrictLauncher");
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                LauncherConfig.Initialize();
                var targetFile = LauncherConfig.PatchFile;
                var targetPath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, targetFile));

                if (!File.Exists(targetPath))
                    throw new FileNotFoundException("Target application not found. Check PatchFile in config.ini.", targetPath);
                if (string.Equals(targetPath, typeof(Program).Assembly.Location, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("PatchFile cannot point to the launcher itself.");

                var targetDirectory = Path.GetDirectoryName(targetPath);
                Directory.SetCurrentDirectory(targetDirectory);
                if (!SetDllDirectoryW(targetDirectory))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set the target application's native DLL search directory.");

                var target = Assembly.LoadFrom(targetPath);
                if (target == typeof(Program).Assembly)
                    throw new InvalidOperationException("PatchFile cannot point to the launcher itself.");

                RuntimeFixes.EntryAssembly = target;
                EmbeddedAssemblies.Install(target);
                _harmony.PatchAll(typeof(Program).Assembly);
                PclLog.Info("Patches installed.");

                PclLog.Info("Starting target application: " + targetPath);
                return InvokeEntryPoint(target, args);
            }
            catch (Exception ex)
            {
                PclLog.Error("Launcher startup failed.", ex);
                Console.Error.WriteLine("Launcher startup failed: " + ex);
                return 1;
            }
        }

        private static int InvokeEntryPoint(Assembly assembly, string[] args)
        {
            var entryPoint = assembly.EntryPoint;
            if (entryPoint == null) throw new InvalidOperationException("The target application has no entry point.");

            var parameters = entryPoint.GetParameters();
            object[] arguments;
            if (parameters.Length == 0) arguments = new object[0];
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                arguments = new object[] { args };
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                arguments = new object[] { string.Join(" ", args) };
            else throw new InvalidOperationException("Unsupported entry point signature: " + entryPoint);

            try
            {
                var result = entryPoint.Invoke(null, arguments);
                return result is int exitCode ? exitCode : 0;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectoryW(string path);
    }
}
