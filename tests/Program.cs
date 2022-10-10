using System;
using System.Collections.Generic;

namespace tests;
internal class Program
{
    static void Main(string[] args)
    {
        string[] teststrings = new[] {
            "a or b and c",
            "(a or b) and c",
            "(a or b xor orc -800) & d ^ e | func -0.15 0.17 and 'thing')" 
        };
        foreach (string teststring in teststrings)
        {
            Console.WriteLine($"{teststring}\n- - - -\n");
            PredicateInlay test = new(teststring, null);
            Console.WriteLine("\n- - - -\n");
        }
    }

}
