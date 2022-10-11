# A midwork for parsing simple logical patterns from text for later evaluation

This item is meant to be used as a pastein singlefile snippet.

## Usage

Drop into your project as a source file.

Feed the pattern string when instantiating. `exchanger` is once for every node in the resulting logic structure. for every "word", exchanger receives a string with word name and zero or more arguments from consequent literals. It should return the `() => bool` type callback.

## Requirements

C#10+, .NET Framework 3.5 or anything later.

## Format

In your expressions, you can use parens and math notation (`!&^|`) as well as english operator words (`not/and/xor/or`) Example of valid expressions:

```
//apparently incomplete parens are alright
a & ( b & (c & (d & e)
((a&b))))))
//you can use operator forms interchangeably
a & b xor c and ! (d or not e)
//here, word a receives binding arguments -0.4 and 15, b receives binding argument ploo. they are all passed as strings
a -0.4 15 or b 'ploo'
//empty parens end up as always true
() or a
```

## Issues
- No error handling, error messages are not informative
- Did not implement compiling to dynmethod yet (maybe will maybe wont)