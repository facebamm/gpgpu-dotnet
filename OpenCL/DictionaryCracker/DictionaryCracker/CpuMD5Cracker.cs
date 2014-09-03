using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DictionaryCracker
{
    public class CpuMD5Cracker : IMD5Cracker
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        private bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        public CpuMD5Cracker()
        {
        }

        public void Warmup()
        {
            string[] strings = { "1234", "12345", "123456", "1234567" };
            var match = Crack(strings.ToList(), "827ccb0eea8a706c4c34a16891f84e7b");
        }

        public string Crack(List<string> plaintext, string target)
        {
            var targetBytes = HexHelper.StringToByteArray(target);
            ThreadLocal<MD5> md5 = new ThreadLocal<MD5>(() => MD5.Create());

            var match = plaintext.AsParallel().Where(p => ByteArrayCompare(targetBytes, md5.Value.ComputeHash(Encoding.Default.GetBytes(p)))).FirstOrDefault();
            return match;
        }

        public string Name
        {
            get { return "CPU MD5 Cracker"; }
        }


        public string Device
        {
            get { return "CPU"; }
        }
    }
}
