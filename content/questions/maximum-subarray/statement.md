Given an array of integers, find the largest sum of any **contiguous, non-empty** subarray.

## Input

- Line 1: the integer `n` (`1 <= n <= 100000`)
- Line 2: `n` integers `a[0] … a[n-1]` (`-10^9 <= a[i] <= 10^9`)

## Output

A single integer: the largest sum.

## Example

Input:

```
9
-2 1 -3 4 -1 2 1 -5 4
```

Output:

```
6
```

The subarray `4 -1 2 1` sums to `6`.

## Note

Walk the array once, keeping the best sum ending at the current position: either extend the
previous one or start again at the current element (Kadane's algorithm). Watch two things — the
answer can be negative when every element is negative, and the sum can exceed a 32-bit integer,
so use 64-bit arithmetic.
