# The I/O is done for you. Fill in max_subarray_sum.
import sys


def max_subarray_sum(nums: list[int]) -> int:
    """The largest sum of any non-empty contiguous subarray."""
    # Your code here.
    return 0


def main() -> None:
    data = sys.stdin.read().split()
    n = int(data[0])
    nums = [int(x) for x in data[1:1 + n]]

    print(max_subarray_sum(nums))


main()
