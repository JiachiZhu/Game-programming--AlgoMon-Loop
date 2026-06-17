using System;
using System.Collections.Generic;

/// <summary>
/// Generic Max-Priority Queue backed by a binary max-heap.
/// The element with the HIGHEST priority value is extracted first.
///
/// Usage in battle: insert each combatant with priority = ClockSpeed.
/// The fastest AlgoMon (highest ClockSpeed) acts first.
///
/// Time complexity:
///   Enqueue  — O(log N)
///   Dequeue  — O(log N)
///   Peek     — O(1)
/// </summary>
// Defense note: PriorityQueue is the main priority queue type used by this part of the project.
public class PriorityQueue<T>
{
    private readonly List<(T item, float priority)> _heap = new List<(T, float)>();

    public int Count => _heap.Count;
    public bool IsEmpty => _heap.Count == 0;

    // Defense note: Runs the enqueue helper used by this script.
    public void Enqueue(T item, float priority)
    {
        _heap.Add((item, priority));
        BubbleUp(_heap.Count - 1);
    }

    // Defense note: Runs the dequeue helper used by this script.
    public T Dequeue()
    {
        if (IsEmpty) throw new InvalidOperationException("PriorityQueue is empty.");

        T top = _heap[0].item;
        int last = _heap.Count - 1;
        _heap[0] = _heap[last];
        _heap.RemoveAt(last);
        if (!IsEmpty) SiftDown(0);
        return top;
    }

    // Defense note: Runs the peek helper used by this script.
    public T Peek()
    {
        if (IsEmpty) throw new InvalidOperationException("PriorityQueue is empty.");
        return _heap[0].item;
    }

    // Defense note: Runs the clear helper used by this script.
    public void Clear() => _heap.Clear();

    // ----------------------------------------------------------------
    // Heap helpers

    // Defense note: Runs the bubble up helper used by this script.
    private void BubbleUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_heap[i].priority <= _heap[parent].priority) break;
            Swap(i, parent);
            i = parent;
        }
    }

    // Defense note: Runs the sift down helper used by this script.
    private void SiftDown(int i)
    {
        int count = _heap.Count;
        while (true)
        {
            int left  = 2 * i + 1;
            int right = 2 * i + 2;
            int largest = i;

            if (left  < count && _heap[left].priority  > _heap[largest].priority) largest = left;
            if (right < count && _heap[right].priority > _heap[largest].priority) largest = right;
            if (largest == i) break;

            Swap(i, largest);
            i = largest;
        }
    }

    // Defense note: Runs the swap helper used by this script.
    private void Swap(int a, int b)
    {
        var tmp = _heap[a];
        _heap[a] = _heap[b];
        _heap[b] = tmp;
    }
}
