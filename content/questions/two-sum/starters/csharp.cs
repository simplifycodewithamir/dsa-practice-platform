// The I/O is done for you. Fill in TwoSum.
public class Program
{
    public static void Main()
    {
        string[] header = Console.ReadLine()!.Split(' ');
        int n = int.Parse(header[0]);
        int target = int.Parse(header[1]);

        int[] nums = Array.ConvertAll(Console.ReadLine()!.Split(' '), int.Parse);

        int[] answer = new Solution().TwoSum(nums, target);
        Console.WriteLine($"{answer[0]} {answer[1]}");
    }
}

public class Solution
{
    /// <summary>The two 0-based positions, smaller first, whose values add up to target.</summary>
    public int[] TwoSum(int[] nums, int target)
    {
        // Your code here.
        return [];
    }
}
