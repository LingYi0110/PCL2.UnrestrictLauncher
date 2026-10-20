using System;
using System.IO;

namespace PCL2.UnrestrictLauncher
{
    internal static class LauncherConfig
    {
        private const string Section = "Launcher";
        private const string DefaultPatchFile = "Plain Craft Launcher 2.exe";
        private static readonly IniFile Ini = new IniFile(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"));

        public static string PatchFile
        {
            get
            {
                var value = Ini.ReadString(Section, "PatchFile", DefaultPatchFile);
                return string.IsNullOrWhiteSpace(value) ? DefaultPatchFile : value;
            }
            set { Ini.WriteString(Section, "PatchFile", value); }
        }

        public static void Initialize()
        {
            if (!File.Exists(Ini.FilePath))
                PatchFile = DefaultPatchFile;
        }
    }
}
