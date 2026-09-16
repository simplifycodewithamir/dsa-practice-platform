// Reference solution: O(n) with a hash map. Ignored by the seeder; kept so the C# runner has a
// known-good submission to check against, the same way reference.py does for Python.
var first = Console.ReadLine()!.Split(' ');
int n = int.Parse(first[0]), target = int.Parse(first[1]);
var values = Console.ReadLine()!.Split(' ').Select(int.Parse).ToArray();
var seen = new Dictionary<int, int>();

for (var i = 0; i < n; i++)
{
    if (seen.TryGetValue(target - values[i], out var j))
    {
        Console.WriteLine($"{j} {i}");
        return;
    }

    seen.TryAdd(values[i], i);
}
