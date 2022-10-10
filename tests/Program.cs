using System;

namespace tests;
internal class Program
{
    static void Main(string[] args)
    {
        string thing = "(a or b xor orc -800) & d ^ e | func -0.15 0.17 and 'thing')";
        Console.WriteLine($"{thing}\n- - - -\n");
        PredicateInlay test = new(thing, null);
        //foreach (var tk in x)
        //{
        //    Console.WriteLine($"{tk.type}, {tk.val}");
        //}
    }
}
