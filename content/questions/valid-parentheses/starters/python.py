# The I/O is done for you. Fill in is_valid.
import sys


def is_valid(s: str) -> bool:
    """True when every bracket in s is closed by the right kind, in the right order."""
    # Your code here.
    return False


def main() -> None:
    s = sys.stdin.readline().strip()

    # The expected output is lowercase, which is not what str(bool) gives you.
    print("true" if is_valid(s) else "false")


main()
