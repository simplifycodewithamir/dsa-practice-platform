Given an array of integers and a target value, find the two **distinct** positions whose values add up to the target.

Exactly one such pair exists in every test.

## Input

- Line 1: two integers `n` and `target` (`2 <= n <= 100000`, `-10^9 <= target <= 10^9`)
- Line 2: `n` integers `a[0] … a[n-1]` (`-10^9 <= a[i] <= 10^9`)

## Output

The two 0-based positions, smaller first, separated by a space.

## Example

Input:

```
4 9
2 7 11 15
```

Output:

```
0 1
```

`a[0] + a[1] = 2 + 7 = 9`.

## Note

A nested loop over every pair is `O(n^2)` and will exceed the time limit on the larger tests.
Store each value you have already seen in a hash map, then look up `target - a[i]` as you go — that
is `O(n)` time and `O(n)` extra space.
