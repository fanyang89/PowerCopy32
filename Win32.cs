using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerCopy32 {
    static class Win32 {
        private const int MaxPath = 255;

        [DllImport("shlwapi.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern bool PathFindOnPath([MarshalAs(UnmanagedType.LPTStr)] StringBuilder pszFile, IntPtr unused);

        public static bool FindInPath(string pszFile, out string fullPath) {
            var sb = new StringBuilder(pszFile, MaxPath);
            var found = PathFindOnPath(sb, IntPtr.Zero);
            fullPath = found ? sb.ToString() : null;
            return found;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetShortPathName(
            [In] string lpFileName,
            [Out] StringBuilder lpBuffer,
            [In] int nBufferLength);

        /// <summary>
        /// Converts a potentially long filename to a short filename.
        /// If the file does not exist, <c>null</c> is returned.
        /// </summary>
        public static string GetShortPathName(string fileName) {
            return GetShortPathName(fileName, MaxPath);
        }

        private static string GetShortPathName(string fileName, int bufferLen) {
            StringBuilder buffer = new StringBuilder(bufferLen);
            int requiredBuffer = GetShortPathName(fileName, buffer, bufferLen);
            if (requiredBuffer == 0) {
                // error... probably file does not exist
                return null;
            }
            if (requiredBuffer > bufferLen) {
                // The buffer we used was not long enough... try again
                return GetShortPathName(fileName, requiredBuffer);
            } else {
                return buffer.ToString();
            }
        }
    }
}
