using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public static class PersistenceHelper
    {
        public static class Clone
        {
            public static byte[] Create()
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

            public static void Create(string path)
            {
                // Create a clone of the application with added arbitrary data to change the hash
                byte[] selfBytes = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
                byte[] arbitraryBytes = GetArbitraryData();
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(selfBytes, 0, selfBytes.Length);
                    stream.Write(arbitraryBytes, 0, arbitraryBytes.Length);
                }

            }

            private static byte[] GetArbitraryData()
            {
                byte[] arbitraryData = new byte[32];
                Random random = new Random();
                random.NextBytes(arbitraryData);
                return arbitraryData;
            }
        }
    }
}
