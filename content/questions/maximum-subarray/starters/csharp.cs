// The I/O is done for you. Fill in MaxSubarraySum.
public class Program
{
    public static void Main()
    {
        int n = int.Parse(Console.ReadLine()!);
        int[] nums = Array.ConvertAll(Console.ReadLine()!.Split(' '), int.Parse);

        Console.WriteLine(new Solution().MaxSubarraySum(nums));
    }
}

public class Solution
{
    /// <summary>The largest sum of any non-empty contiguous subarray. Can overflow int -- hence long.</summary>
    public long MaxSubarraySum(int[] nums)
    {
        // Your code here.
        return 0;
    }
}
