using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PCL2.UnrestrictLauncher
{
    internal sealed class IniFile
    {
        public string FilePath { get; private set; }

        public IniFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("An INI file path is required.", "filePath");

            FilePath = Path.GetFullPath(filePath);
        }

        public string ReadString(string section, string key, string defaultValue = "")
        {
            ValidateSectionAndKey(section, key);
            if (defaultValue == null)
                throw new ArgumentNullException("defaultValue");

            // Retry with a larger buffer instead of silently truncating the value.
            for (int capacity = 256; ; capacity = checked(capacity * 2))
            {
                var buffer = new StringBuilder(capacity);
                uint length = GetPrivateProfileStringW(section, key, defaultValue,
                    buffer, (uint)capacity, FilePath);
                if (length < capacity - 1)
                    return buffer.ToString();
            }
        }

        public void WriteString(string section, string key, string value)
        {
            ValidateSectionAndKey(section, key);
            if (value == null)
                throw new ArgumentNullException("value");

            // The Windows API preserves Unicode only if the file already has a BOM.
            using (var stream = new FileStream(FilePath, FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.Read))
            {
                if (stream.Length == 0)
                {
                    byte[] preamble = Encoding.Unicode.GetPreamble();
                    stream.Write(preamble, 0, preamble.Length);
                }
            }

            if (!WritePrivateProfileStringW(section, key, value, FilePath))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Failed to write the INI file: " + FilePath);
        }

        private static void ValidateSectionAndKey(string section, string key)
        {
            if (string.IsNullOrWhiteSpace(section))
                throw new ArgumentException("An INI section is required.", "section");
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("An INI key is required.", "key");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetPrivateProfileStringW(string section, string key,
            string defaultValue, StringBuilder returnedString, uint size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileStringW(string section, string key,
            string value, string filePath);
    }
}
