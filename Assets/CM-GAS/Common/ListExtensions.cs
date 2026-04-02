using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static Unity.VisualScripting.Member;
using static UnityEngine.GraphicsBuffer;

public static class ListExtensions
{
    public static int AddUnique<T>(this List<T> target, T item)
    {
        if (target.Contains(item))
            return -1;

        target.Add(item);
        return target.Count - 1;
    }

    public static void MoveTemp<T>(this List<T> target, List<T> other)
    {
        if (target == null || other == null || target == other)
            return;

        target.Clear();
        target.AddRange(other);
        other.Clear();
    }

    public static int RemoveSingle<T>(this List<T> list, T item)
    {
        int index = list.IndexOf(item);
        if (index >= 0)
        {
            list.RemoveAt(index);
        }
        return index;
    }

    public static bool IsValidIndex<T>(this List<T> list, int idx)
    {
        if (list == null) return false;
        return idx < 0 || idx >= list.Count;
    }
}
