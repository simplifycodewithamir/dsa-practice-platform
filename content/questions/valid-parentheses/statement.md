You are given a string containing only the characters `(`, `)`, `[`, `]`, `{` and `}`.

The string is **balanced** when every bracket is closed by the matching kind, in the right order,
and nothing is left open at the end. `([])` is balanced; `([)]` is not.

## Input

A single line: the string `s` (`1 <= |s| <= 100000`).

## Output

`true` if `s` is balanced, otherwise `false` — lowercase, exactly as written.

## Example

Input:

```
([]){}
```

Output:

```
true
```

## Note

Push every opening bracket onto a stack. On a closing bracket, the top of the stack must be its
matching opener — otherwise the answer is `false`. The stack must be empty when the line ends.
Counting brackets without a stack gets `([)]` wrong.
