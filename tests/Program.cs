using System;
using System.Collections.Generic;

namespace tests;
internal class Program
{
    static void Main(string[] args)
    {
        string[] teststrings = new[] {
            //"a or b and c 12 13 15 -16 'aegew2' -17.2",
            "(a xor not b) and c",
            "(a) and (b) xor (c | d)",
            "(a or b xor c -800) & d ^ e | (f -0.15 0.17 and g 'thing')",
        };

        PredicateInlay.del_FetchPred exchanger = (name, args) =>
        {
            return () => {
                return name switch
                {
                    "a" => true,
                    "b" => false,
                    "c" => true,
                    "d" => false,
                    "e" => true,
                    "f" => false,
                    "g" => true,
                    "h" => false,
                    _ => true
                };
                //''return true;
            };
        };

        foreach (string teststring in teststrings)
        {

            Console.WriteLine($"{teststring}\n- - - -\n");
            PredicateInlay test = new(teststring, exchanger);
            Console.WriteLine( test.TheTree.Eval());
            Console.WriteLine("\n- - - -\n");
        }
    }

}
