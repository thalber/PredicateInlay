using System;
using System.Collections.Generic;

namespace tests;
internal class Program
{
    static void Main(string[] args)
    {
        string[] teststrings = new[] {
            "not (a))))))))",
            "(((((a)",
            "() xor a",
            "a -0.4 15 or b 'ploo'",
            "a & b xor c and ! (d or not e)",
            "(a xor not b) and c",
            "(a) and (b) xor (c | d)",
            "(a or b xor c -800) & d ^ e | (f -0.15 0.17 and g 'thing')",
            "a 'abcde' or b '-10 -10 -10 -10' or c '10;10;10'"
        };

        PredicateInlay.del_FetchPred exchanger = (name, args) =>
        {
            return () =>
            {
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
            };
        };

        foreach (string teststring in teststrings)
        {

            Console.WriteLine($"{teststring}");
            PredicateInlay test = new(teststring, exchanger);
            Console.WriteLine(test.Eval());
            Console.WriteLine("\n- - - -\n");
        }
    }

}
