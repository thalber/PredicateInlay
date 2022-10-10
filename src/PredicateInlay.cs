﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
/// <summary>
/// Parses a string by logical expression operators (<c>()^|+ </c>) into an expression, then, using a supplied <see cref="del_FetchPred"/>, exchanges resulting substrings between logical operators into delegates, then evaluates expression on demand.
/// </summary>
public sealed class PredicateInlay
{
    #region fields
    //public Tree ltree;
    //private Dictionary<string, del_Pred> WordBindings
    #endregion
    public PredicateInlay(string expression, del_FetchPred exchanger)
    {
        Token[] tokens = Tokenize(expression).ToArray();
        //ltree = new(tokens);
        int index = 0;
        var x = Parse(tokens, ref index);
        Console.WriteLine(x.ToString(null, null));
    }

    private static IExpr Parse(Token[] tokens, ref int index)
    {
        Group release = new Group();
        //int indent = 0;
        List<string> litStack = new();
        int prevWordIndex = index;
        string? cWord = null;
        //Token? prevWord = null
        List<IExpr> branches = new();
        //IExpr? PrevNode = null;
        for (; index < tokens.Length; index++)
        {
            
            //see what current token is
            ref var cTok = ref tokens[index];
            if (cTok.type is not TokenType.Literal && cWord is not null)
            {
                FinalizeWord();
            }

            switch (cTok.type)
            {
                //if it's a delim, recurse into an embedded group
                case TokenType.DelimOpen:
                    index += 1;
                    branches.Add(Parse(tokens, ref index));
                    break;
                case TokenType.DelimClose:
                    if (cWord is not null) FinalizeWord();
                    goto finish;
                //if it's an operator, push an operator
                case TokenType.Operator:
                    branches.Add(new Oper(GetOp(in cTok), null, null));
                    break;
                case TokenType.Word:
                    prevWordIndex = index;
                    cWord = cTok.val;
                    break;
                default:
                    break;
            }
        }
    finish:
        if (cWord is not null) FinalizeWord();

        foreach (Op tp in new[] { Op.AND, Op.XOR, Op.OR })
        {
            for (int i = branches.Count - 1; i >= 0; i--)
            {
                var cBranch = branches[i];
                if (cBranch is Oper o && o.TP == tp)
                {
                    if (i < 0 || i >= branches.Count) continue;
                    o.R = branches[i + 1];
                    o.L = branches[i - 1];
                    branches[i] = o;
                    branches.RemoveAt(i + 1);
                    branches.RemoveAt(i - 1);
                    i--;
                }
            }

        }

        void FinalizeWord()
        {
            branches.Add(MakeLeaf(tokens, in prevWordIndex));
            cWord = null;
            litStack.Clear();
        }

        //foreach (var t in branchesFlat) Console.WriteLine(t.ToString(null, null));
        release.members = branches.ToArray();
        return release;
    }

    private static List<Token> Tokenize(string expression)
    {
        List<Token> tokens = new();
        string remaining = expression.Clone() as string;
        List<KeyValuePair<TokenType, Match>> results = new();
        while (remaining.Length > 0)
        {
            results.Clear();
            //closest match is considered the correct one.
            int closest = int.MaxValue;
            //token recognition precedence set by enum order.
            foreach (TokenType val in Enum.GetValues(typeof(TokenType)))
            {
                var matcher = exes[val];
                var match = matcher.Match(remaining);
                if (match.Success)
                {
                    //something found.
                    if (match.Index < closest) closest = match.Index;
                    results.Add(new(val, match));
                }
            }

            KeyValuePair<TokenType, Match>? sel = null;
            for (int i = 0; i < results.Count; i++) {
                KeyValuePair<TokenType, Match> kvp = results[i]; 
                if (kvp.Value.Index == closest) { sel = kvp; break; }
            }
            //no tokens recognzed, abort
            //todo: maybe just break?
            if (sel == null)
                throw new ArgumentException($"encountered a parsing error (remaining: {remaining})");
            //cut the remaining string, add gathered token if not a separator.
            var tt = sel.Value.Key;
            var selMatch = sel.Value.Value;
            remaining = remaining.Substring(selMatch.Index + selMatch.Length);
            if (sel.Value.Key != TokenType.Separator)
            {
                tokens.Add(new Token(tt, selMatch.Value));
            }
        }
        return tokens;
    }

    #region nested
    
    public class Tree
    {

    }

    public interface IExpr : IFormattable
    {
        public bool Eval();
        public void Populate(del_FetchPred exchanger);
    }
    [System.Diagnostics.DebuggerDisplay("{ToString(null, null)}")]

    public struct Leaf : IExpr
    {
        public readonly string funcName;
        public readonly string[] args;
        public del_Pred? myPredicate { get; private set; }
        public Leaf(string funcName, string[] args)
        {
            this.funcName = funcName;
            this.args = args;
            myPredicate = null;
        }

        //public Leaf(string name, string[] args){
        public bool Eval()
            => myPredicate?.Invoke() ?? true;

        public void Populate(del_FetchPred exchanger)
        {
            myPredicate = exchanger(funcName, args);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return "Leaf " + funcName + "(" + (args.Length == 0 ? String.Empty : args.Aggregate((x, y) => $"{x}, {y}")) + ")";
        }
    }
    [System.Diagnostics.DebuggerDisplay("{ToString(null, null)}")]
    public struct Group : IExpr
    {
        public IExpr[] members;
        public bool Eval()
        {
            throw new NotImplementedException();
        }

        public void Populate(del_FetchPred exchanger)
        {
            throw new NotImplementedException();
        }
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return "{" + (members.Length == 0 ? String.Empty : members.Select(x => x.ToString(null, null)).Aggregate((x, y) => $"{x}, {y}")) + "}";
        }
    }
    [System.Diagnostics.DebuggerDisplay("{ToString(null, null)}")]
    public struct Oper : IExpr
    {
        public Op TP;
        public IExpr L;
        public IExpr R;

        public Oper(Op tP, IExpr l, IExpr r)
        {
            TP = tP;
            L = l;
            R = r;
        }

        //public readonly 
        public bool Eval()
            => TP switch
            {
                Op.AND => L.Eval() && R.Eval(),
                Op.OR => L.Eval() || R.Eval(),
                Op.XOR => L.Eval() ^ R.Eval(),
                //Op.NOT => throw new NotImplementedException(),
                _ => throw new ArgumentException("Invalid operator"),
            };

        public void Populate(del_FetchPred exchanger)
        {
            L.Populate(exchanger);
            R.Populate(exchanger);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return $"[{L} {this.TP} {R}]";
        }
    }

    [System.Diagnostics.DebuggerDisplay("{type}:\"{val}\"")]
    public struct Token
    {
        public TokenType type;
        public string val;

        public Token(TokenType type, string val)
        {
            this.type = type;
            this.val = val;
        }
    }

    public enum Op
    {
        AND,
        OR,
        XOR,
        //NOT
    }
    public enum TokenType
    {
        DelimOpen,
        DelimClose,
        Separator,
        Operator,
        Word,
        Literal,
        //Discard
    }

    public delegate bool del_Pred();
    public delegate del_Pred del_FetchPred(string name, params string[] args);
    #endregion
    #region statics
    public static Leaf? MakeLeaf(Token[] tokens, in int index)
    {
        if (index < 0 || index >= tokens.Length) return null;
        Token tok = tokens[index];
        if (tok.type != TokenType.Word) return null;

        List<string> args = new();
        for (int i = index + 1; i < tokens.Length; i++)
        {
            var argque = tokens[i];
            if (argque.type != TokenType.Literal) break;
            args.Add(argque.val);
        }
        return new Leaf(tok.val, args.ToArray());
    }
    public static Op GetOp(in Token t)
    {
        if (t.type != TokenType.Operator) throw new ArgumentException("Incorrect token type!");
        return t.val.ToLower() switch
        {
            "|" or "or" => Op.OR,
            "&" or "and" => Op.AND,
            "^" or "xor" or "!=" => Op.XOR,
            "!" or "not" => throw new ArgumentException("Invert operator currently not supported"),
            _ => throw new ArgumentException("Invalid token payload")
        };
    }
    private static Regex RegexForTT(TokenType tt)
        => tt switch
        {
            //todo: decide on delims usage
            TokenType.DelimOpen => new Regex("[([{]", RegexOptions.Compiled),
            TokenType.DelimClose => new Regex("[)\\]}]", RegexOptions.Compiled),
            TokenType.Separator => new Regex("[_\\s,]+", RegexOptions.Compiled),
            TokenType.Operator => new Regex("!=|[&|^!]|(and|or|xor|not)(?=\\s)", RegexOptions.Compiled),
            TokenType.Word => new Regex("[a-zA-Z]+", RegexOptions.Compiled),
            TokenType.Literal => new Regex("-{0,1}\\d+(\\.\\d+){0,1}|(?<=').+(?=')", RegexOptions.Compiled),
            //TokenType.Discard => throw new NotImplementedException(),
            _ => throw new IndexOutOfRangeException("Supplied invalid token type"),
        };
    private readonly static Dictionary<TokenType, Regex> exes;
    static PredicateInlay()
    {
        exes = new();
        foreach (TokenType val in Enum.GetValues(typeof(TokenType))) exes.Add(val, RegexForTT(val));
    }
    #endregion
}
