namespace ControlR.Libraries.Shared.Extensions;

public static class ListExtensions
{
    public static bool TryReplace<T>(this IList<T> self, T newItem, Predicate<T> predicate)
    {
        if (newItem is null)
        {
            return false;
        }

        var foundItem = self.FirstOrDefault(x => predicate(x));

        if (foundItem is null)
        {
            return false;
        }

        var index = self.IndexOf(foundItem);

        if (index == -1)
        {
            return false;
        }

        self[index] = newItem;
        return true;
    }

    public static void RemoveDuplicates<T>(this List<T> self)
    {
        var distinct = self.Distinct().ToList();
        self.Clear();
        self.AddRange(distinct);
    }
}
