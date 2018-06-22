using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NtpTimeSyncService.Utilities
{
    public class IniFile
    {
        public string strPath;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniFile(string INIPath)
        {
            strPath = INIPath;
        }

        public string read(string section, string key, string DefaultValue)
        {
            var val = read(section, key);
            return string.IsNullOrEmpty(val) ? DefaultValue : val;
        }

        public int read(string section, string key, int DefaultValue)
        {
            var val = read(section, key);
            return string.IsNullOrEmpty(val) ? DefaultValue : Convert.ToInt32(val);
        }

        public bool read(string section, string key, bool DefaultValue)
        {
            var val = read(section, key);
            val = val.ToLower();
            if (string.IsNullOrEmpty(val)) return DefaultValue;
            if (val.Equals("0") || val.Equals("false") || val.Equals("f")) return false;
            if (val.Equals("1") || val.Equals("true") || val.Equals("t")) return true;
            return false;
        }

        public string read(string section, string key)
        {
            StringBuilder sbTemp = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, "", sbTemp, 255, this.strPath);
            return sbTemp.ToString();
        }

        public void write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, this.strPath);
        }
    }
    
    
}
