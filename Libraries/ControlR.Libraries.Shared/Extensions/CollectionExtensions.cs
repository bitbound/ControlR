using System.Collections.ObjectModel;

namespace ControlR.Libraries.Shared.Extensions;

public static class CollectionExtensions
{
    public static void AddRange<T>(this ObservableCollection<T> self, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            self.Add(item);
        }
    }

    public static void RemoveAll<T>(this ObservableCollection<T> self, Predicate<T> predicate)
    {
        var items = self
            .Where(x => predicate(x))
            .ToArray();

        foreach (var item in items)
        {
            self.Remove(item);
        }
    }

    public static string StringJoin(this IEnumerable<string> self, string separator)
    {
        return string.Join(separator, self);
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
    {
        await Task.Yield();

        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    public static bool TryFindIndex<T>(this ObservableCollection<T> self, Predicate<T> predicate, out int index)
    {
        index = -1;
        var item = self.FirstOrDefault(x => predicate(x));

        if (item is null)
        {
            return false;
        }

        index = self.IndexOf(item);
        return index > -1;
    }
}