using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerCopy32 {
    static class Win32 {
        [DllImport("shlwapi.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern bool PathFindOnPath([MarshalAs(UnmanagedType.LPTStr)] StringBuilder pszFile, IntPtr unused);

        public static bool FindInPath(string pszFile, out string fullPath) {
            var sb = new StringBuilder(pszFile, 260);
            var found = PathFindOnPath(sb, IntPtr.Zero);
            fullPath = found ? sb.ToString() : null;
            return found;
        }
    }
}
