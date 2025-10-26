using System.Collections;

namespace ControlR.Libraries.Shared.Collections;

public class ConcurrentHashSet<T> : ISet<T>, IReadOnlySet<T>
{
  private readonly HashSet<T> _set = [];

  public ConcurrentHashSet()
  { }

  public ConcurrentHashSet(IEnumerable<T> initialItems)
  {
    foreach (var item in initialItems)
    {
      _set.Add(item);
    }
  }

  public int Count
  {
    get
    {
      lock (_set)
      {
        return _set.Count;
      }
    }
  }

  public bool IsReadOnly => false;

  public bool Add(T item)
  {
    lock (_set)
    {
      return _set.Add(item);
    }
  }

  public void Clear()
  {
    lock (_set)
    {
      _set.Clear();
    }
  }

  public bool Contains(T item)
  {
    lock (_set)
    {
      return _set.Contains(item);
    }
  }

  public void CopyTo(T[] array, int arrayIndex)
  {
    lock (_set)
    {
      _set.CopyTo(array, arrayIndex);
    }
  }

  public void ExceptWith(IEnumerable<T> other)
  {
    lock (_set)
    {
      _set.ExceptWith(other);
    }
  }

  public IEnumerator<T> GetEnumerator()
  {
    lock (_set)
    {
      return _set.ToHashSet().GetEnumerator();
    }
  }

  public void IntersectWith(IEnumerable<T> other)
  {
    lock (_set)
    {
      _set.IntersectWith(other);
    }
  }

  public bool IsProperSubsetOf(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.IsProperSubsetOf(other);
    }
  }

  public bool IsProperSupersetOf(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.IsProperSupersetOf(other);
    }
  }

  public bool IsSubsetOf(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.IsSubsetOf(other);
    }
  }

  public bool IsSupersetOf(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.IsSupersetOf(other);
    }
  }

  public bool Overlaps(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.Overlaps(other);
    }
  }

  public bool Remove(T item)
  {
    lock (_set)
    {
      return _set.Remove(item);
    }
  }

  public int RemoveWhere(Predicate<T> match)
  {
    lock (_set)
    {
      return _set.RemoveWhere(match);
    }
  }

  public void SymmetricExceptWith(IEnumerable<T> other)
  {
    lock (_set)
    {
      _set.SymmetricExceptWith(other);
    }
  }

  public void UnionWith(IEnumerable<T> other)
  {
    lock (_set)
    {
      _set.UnionWith(other);
    }
  }

  void ICollection<T>.Add(T item)
  {
    lock (_set)
    {
      _ = _set.Add(item);
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.SetEquals(other);
    }
  }

  bool ISet<T>.SetEquals(IEnumerable<T> other)
  {
    lock (_set)
    {
      return _set.SetEquals(other);
    }
  }
}