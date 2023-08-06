using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderNoteD3D12;

public class LRUCacheEventArgs<T1, T2> : EventArgs
{
    public T1 Key { get; set; }
    public T2 Value { get; set; }
}

public class LRUCache<T1, T2>
{
    LinkedList<(T1, T2)> cache = new();
    Dictionary<T1, LinkedListNode<(T1, T2)>> nodes = new();

    public LRUCache(int capacity = 256, IEqualityComparer<T1> equalityComparer = null)
    {
        Capacity = capacity;
        nodes = new(equalityComparer);
    }

    public int Capacity { get; set; } = 256;

    public event EventHandler<LRUCacheEventArgs<T1, T2>> Deactivating;

    public T2 GetObject(T1 key, Func<T1, T2> whenNotExist)
    {
        if (nodes.TryGetValue(key, out var node))
        {
            cache.Remove(node);
            cache.AddFirst(node);

            return node.Value.Item2;
        }
        else
        {
            if (cache.Count >= 256)
            {
                var last = cache.Last();
                nodes.Remove(last.Item1);
                cache.Remove(last);
                Deactivating?.Invoke(this, new LRUCacheEventArgs<T1, T2>()
                {
                    Key = key,
                    Value = last.Item2
                });
            }
            var newValue = whenNotExist(key);
            var newNode = cache.AddFirst((key, newValue));
            nodes[key] = newNode;

            return newValue;
        }
    }

    public IEnumerable<T2> Values
    {
        get
        {
            foreach (var item in cache)
            {
                yield return item.Item2;
            }
        }
    }

    public void InvalidCache(T1 key)
    {
        if (nodes.TryGetValue(key, out var node))
        {
            nodes.Remove(key);
            cache.Remove(node);
            Deactivating?.Invoke(this, new LRUCacheEventArgs<T1, T2>()
            {
                Key = key,
                Value = node.Value.Item2
            });
        }
    }
}