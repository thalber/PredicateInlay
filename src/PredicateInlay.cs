using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using System.Reflection.Emit;
/// <summary>
/// Parses a string by logical expression operators (symbolic as well as words and/or/xor/not) into a treelike structure, then, using a supplied <see cref="del_FetchPred"/>, exchanges resulting substrings between logical operators into delegates, then evaluates expression on demand.
/// </summary>
public sealed partial class PredicateInlay
{
    //todo: compiling to a dynamic method?
    #region fields
    /// <summary>
    /// Contains the Inlay's logic tree
    /// </summary>
    private readonly Tree TheTree;
    private Guid myID = Guid.NewGuid();
    private DynamicMethod? compiledEvalDynM;
    private del_Pred? compiledEval;
    #endregion
    public PredicateInlay(string expression, del_FetchPred exchanger)
    {
        //tokenize
        Token[] tokens = Tokenize(expression).ToArray();
        //prepare the soil
        int index = 0;
        var x = Parse(tokens, ref index);
        //plant the tree
        TheTree = new(x);
        //water it
        TheTree.Populate(exchanger);
    }
    #region user methods
    public void Populate(del_FetchPred newExchanger) { 
        TheTree.Populate(newExchanger);
        compiledEvalDynM = null;
        compiledEval = null;
    }
    public void Compile()
    {
        if (TheTree is null) return;
        if (!TheTree.Populated) return;
        DynamicMethod dyn = new(
            $"PredInlay-{myID}-DynEval",
            typeof(bool),
            new Type[0],
            typeof(PredicateInlay),
            false);
        var il = dyn.GetILGenerator();
        EmitToDyn(ref il, in TheTree.root);
    }
    public bool Eval() => compiledEval?.Invoke() ?? TheTree.Eval();
    #endregion
    #region internals
    private void EmitToDyn(ref ILGenerator il, in IExpr ex)
    {
        //todo: remove or finish
        throw new NotImplementedException();
    }
    /// <summary>
    /// Tokenizes a string.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns>An array of tokens.</returns>
    public static List<Token> Tokenize(string expression)
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
            //scroll through all acquired results, take the closest one with higher precedence.
            KeyValuePair<TokenType, Match>? selectedKvp = null;
            for (int i = 0; i < results.Count; i++) {
                KeyValuePair<TokenType, Match> kvp = results[i]; 
                if (kvp.Value.Index == closest) { selectedKvp = kvp; break; }
            }
            //no recognizable patterns left, abort.
            if (selectedKvp == null)
                break;
            //cut the remaining string, add gathered token if not a separator.
            var tokType = selectedKvp.Value.Key;
            var selectedMatch = selectedKvp.Value.Value; //fuck these things get ugly
            remaining = remaining.Substring(selectedMatch.Index + selectedMatch.Length);
            if (selectedKvp.Value.Key != TokenType.Separator)
            {
                tokens.Add(new Token(tokType, selectedMatch.Value));
            }
        }
        return tokens;
    }
    /// <summary>
    /// Recursive descent parsing.
    /// </summary>
    /// <param name="tokens">An array of tokens to work over.</param>
    /// <param name="index">A reference to current index. Obviously, top layer should start at zero.</param>
    /// <returns>The resulting <see cref="IExpr"/>.</returns>
    /// <exception cref="InvalidOperationException">Failed to strip a group.</exception>
    private IExpr Parse(Token[] tokens, ref int index)
    {
        if (tokens.Length == 0) return new Stub();
        List<string> litStack = new();
        int prevWordIndex = index; //index of a last word, used for finalizing words
        string? cWord = null; //current word's name
        List<IExpr> branches = new();
        for (; index < tokens.Length; index++)
        {
            //see what current token is
            ref var cTok = ref tokens[index];
            if (cTok.type is not TokenType.Literal && cWord is not null)
            {
                FinalizeWord(); //a word's arguments have ended.
            }

            switch (cTok.type)
            {
                //if it's a delim, recurse into an embedded group
                case TokenType.DelimOpen:
                    index += 1;
                    branches.Add(Parse(tokens, ref index)); //descend
                    break;
                case TokenType.DelimClose:
                    if (cWord is not null) FinalizeWord();
                    goto finish; // round up
                    //if it's an operator, push an operator
                case TokenType.Operator:
                    branches.Add(new Oper(GetOp(in cTok).Value));
                    break;
                    //begin recording a new word
                case TokenType.Word:
                    prevWordIndex = index;
                    cWord = cTok.val;
                    break;
                default:
                    break;
            }
        }
    finish:
        if (cWord is not null) FinalizeWord(); //just to be sure
        //operators start consuming
        foreach (Op tp in new[] { Op.NOT, Op.AND, Op.XOR, Op.OR })
        {
            //looping right to left.
            for (int i = branches.Count - 1; i >= 0; i--)
            {
                IExpr cBranch = branches[i];
                if (cBranch is Oper o && o.TP == tp && o.L is null && o.R is null)
                {
                    if (i < 0 || i >= branches.Count) continue;
                    if (o.TP is not Op.NOT)
                    {
                        //remove both
                        o.R = branches[i + 1];  
                        o.L = branches[i - 1];
                        branches[i] = o;
                        branches.RemoveAt(i + 1);
                        branches.RemoveAt(i - 1);
                        i--;
                    }
                    else
                    {
                        //only on the right
                        o.R = branches[i + 1];
                        branches[i] = o;
                        branches.RemoveAt(i + 1);
                    }
                }
            }
        }

        //for (int i = branches.Count - 1; i >= 0; i--)
        //{
        //    IExpr cBranch = branches[i];
        //    if (cBranch is Oper o && o.L == null) { branches[i] = o.R; }
        //}
        return branches.Count switch
        {
            0 => new Stub(), // empty group
            1 => branches[0], // normal
            _ => throw new InvalidOperationException("Can't abstract away group!"), //failed to strip
        };
        void FinalizeWord()
        {
            branches.Add(MakeLeaf(tokens, in prevWordIndex));
            cWord = null;
            litStack.Clear();
        }
    }
    /// <summary>
    /// Attempts to create a leaf node from a selected token, using all subsequent literals as args.
    /// </summary>
    /// <param name="tokens">Token array.</param>
    /// <param name="index">Token index.</param>
    /// <returns>Resulting leaf node, null if failure</returns>
    #endregion
    #region nested
    /// <summary>
    /// Wraps compiled expression structure
    /// </summary>
    public class Tree
    {
        public bool Populated { get; private set; }
        /// <summary>
        /// root node
        /// </summary>
        public readonly IExpr root;
        public Tree(IExpr root)
        {
            this.root = root;
        }
        /// <summary>
        /// Fills the expression using given <see cref="del_FetchPred"/>
        /// </summary>
        /// <param name="exchanger"></param>
        public void Populate(del_FetchPred exchanger)
        {
            Populated = true;
            root.Populate(exchanger);
        }
        /// <summary>
        /// Runs evaluation on a tree
        /// </summary>
        /// <returns></returns>
        public bool Eval() => root.Eval();
    }
    /// <summary>
    /// Base interface for expressions
    /// </summary>
    public interface IExpr
    {
        /// <summary>
        /// Evaluates a node and checks if it's true or false. Ran repeatedly.
        /// </summary>
        /// <returns></returns>
        public bool Eval();
        /// <summary>
        /// Populates a node (and children nodes if any) using a given <see cref="del_FetchPred"/>. Ran once.
        /// </summary>
        /// <param name="exchanger"></param>
        public void Populate(del_FetchPred exchanger);
    }
    /// <summary>
    /// Empty node, always returns true
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public struct Stub : IExpr
    {
        public bool Eval() => true;
        public void Populate(del_FetchPred exchanger) { }
        public override string ToString() => "{}";
    }
    /// <summary>
    /// An end node; carries parameters passed when parsing and a final callback reference. If the callback is null, always true.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public struct Leaf : IExpr
    {
        public readonly string funcName;
        public readonly string[] args;
        public del_Pred? myCallback { get; private set; }
        public Leaf(string funcName, string[] args)
        {
            this.funcName = funcName;
            this.args = args;
            myCallback = null;
        }
        public bool Eval() => myCallback?.Invoke() ?? true;
        public void Populate(del_FetchPred exchanger) => myCallback = exchanger(funcName, args);
        public override string ToString()
        {
            return funcName + "(" + (args.Length == 0 ? string.Empty : args.Aggregate((x, y) => $"{x}, {y}")) + ")";
        }
    }
    /// <summary>
    /// A compile time node. should always be stripped when finishing parse.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public struct Group : IExpr
    {
        public IExpr[] members;
        public bool Eval()
        {
            throw new InvalidOperationException("Groups should not exist!");
        }
        public void Populate(del_FetchPred exchanger)
        {
            throw new InvalidOperationException("Groups should not exist!");
        }
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return "{" + ((members?.Length ?? 0) == 0 ? String.Empty : members.Select(x => x.ToString()).Aggregate((x, y) => $"{x}, {y}")) + "}";
        }
    }
    /// <summary>
    /// An operator. Can have one or two operands (if one, it's always on the right).
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public struct Oper : IExpr
    {
        /// <summary>
        /// operation type
        /// </summary>
        public Op TP;
        /// <summary>
        /// Left operand
        /// </summary>
        public IExpr? L;
        /// <summary>
        /// right operand
        /// </summary>
        public IExpr R;
        public Oper(Op tP)
        {
            TP = tP;
            L = null;
            R = null;
        }
        public bool Eval()
            => TP switch
            {
                Op.AND => (L?.Eval() ?? true) && (R?.Eval() ?? true),
                Op.OR => (L?.Eval() ?? true) || (R?.Eval() ?? true),
                Op.XOR => (L?.Eval() ?? true) ^ (R?.Eval() ?? true),
                Op.NOT => ! (R?.Eval() ?? true),
                _ => throw new ArgumentException("Invalid operator"),
            };
        public void Populate(del_FetchPred exchanger)
        {
            L?.Populate(exchanger);
            R.Populate(exchanger);
        }

        public override string ToString()
        {
            return $"[ {L} {TP} {R} ]";
        }
    }
    /// <summary>
    /// A parsing token. Carries type and value.
    /// </summary>
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
    /// <summary>
    /// Operation type.
    /// </summary>
    public enum Op
    {
        NOT,
        AND,
        XOR,
        OR,
    }
    /// <summary>
    /// Token type. Order of enum items determines recognition precedence.
    /// </summary>
    public enum TokenType
    {
        DelimOpen,
        DelimClose,
        Separator,
        Operator,
        Literal,
        Word,
        //Discard
    }
    /// <summary>
    /// Eval invocation delegates.
    /// </summary>
    /// <returns></returns>
    public delegate bool del_Pred();
    /// <summary>
    /// Eval invocation delegates retrieval delegates. =
    /// </summary>
    /// <param name="name">Function name.</param>
    /// <param name="args">Arguments.</param>
    /// <returns>Delegate for selected word. Returning null is allowed.</returns>
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
    /// <summary>
    /// Gets an operator type from token 
    /// </summary>
    /// <param name="t">Token to check</param>
    /// <returns>Resulting operation type, null if token was not an operator token.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static Op? GetOp(in Token t)
    {
        if (t.type != TokenType.Operator) return null;
        return t.val.ToLower() switch
        {
            "|" or "or" => Op.OR,
            "&" or "and" => Op.AND,
            "^" or "xor" or "!=" => Op.XOR,
            "!" or "not" => Op.NOT,
            _ => throw new ArgumentException("Invalid token payload")
        };
    }
    /// <summary>
    /// Modify regex options
    /// </summary>
    public static RegexOptions compiled = RegexOptions.Compiled;
    /// <summary>
    /// Returns a recognition regex object for a given token type.
    /// </summary>
    /// <param name="tt"></param>
    /// <returns></returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    private static Regex RegexForTT(TokenType tt)
    {
        return tt switch
        {
            //todo: decide on delims usage
            TokenType.DelimOpen     => new Regex("[([{]", compiled),
            TokenType.DelimClose    => new Regex("[)\\]}]", compiled),
            TokenType.Separator     => new Regex("[\\s,]+", compiled),
            TokenType.Operator      => new Regex("!=|[&|^!]|(and|or|xor|not)(?=\\s)", compiled),
            TokenType.Literal       => new Regex("-?\\d+(\\.\\d+)?|(?<=').*(?=')", compiled),
            TokenType.Word          => new Regex("[a-zA-Z_]+", compiled),
            //TokenType.Discard => throw new NotImplementedException(),
            _ => throw new IndexOutOfRangeException("Supplied invalid token type"),
        };
    }
    /// <summary>
    /// precached token rec regexes
    /// </summary>
    private readonly static Dictionary<TokenType, Regex> exes;
    static PredicateInlay()
    {
        exes = new();
        foreach (TokenType val in Enum.GetValues(typeof(TokenType))) exes.Add(val, RegexForTT(val));
    }
    #endregion
}
