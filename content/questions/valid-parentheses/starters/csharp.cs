// The I/O is done for you. Fill in IsValid.
public class Program
{
    public static void Main()
    {
        string s = Console.ReadLine() ?? string.Empty;

        Console.WriteLine(new Solution().IsValid(s) ? "true" : "false");
    }
}

public class Solution
{
    /// <summary>True when every bracket in s is closed by the right kind, in the right order.</summary>
    public bool IsValid(string s)
    {
        // Your code here.
        return false;
    }
}
