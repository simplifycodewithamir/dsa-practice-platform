# The I/O is done for you. Fill in two_sum.
import sys


def two_sum(nums: list[int], target: int) -> list[int]:
    """The two 0-based positions, smaller first, whose values add up to target."""
    # Your code here.
    return []


def main() -> None:
    data = sys.stdin.read().split()
    n, target = int(data[0]), int(data[1])
    nums = [int(x) for x in data[2:2 + n]]

    answer = two_sum(nums, target)
    print(answer[0], answer[1])


main()
