namespace DsaPractice.DataAccess.Enums;

// Persisted by name (see QuestionConfiguration), so reordering or inserting members is safe.
// Side effect: ORDER BY on the column sorts alphabetically (Easy, Hard, Medium) -- sort in code
// or map to a rank if difficulty ordering is ever needed in a query.
public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard
}
