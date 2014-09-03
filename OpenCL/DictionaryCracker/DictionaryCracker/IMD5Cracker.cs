using System;
using System.Collections.Generic;
namespace DictionaryCracker
{
    public interface IMD5Cracker
    {
        void Warmup();
        string Crack(List<string> plaintext, string target);
        string Name { get; }
        string Device { get; }
    }
}
