using System;
using System.Collections.Generic;

public class GenericPool<T>
{
    private readonly Queue<T> _availableOccurrences = new();
    private Func<T> _createNew;
    private Action<T> _reset;

    public GenericPool(Func<T> createNew, Action<T> reset)
    {
        _createNew = createNew;
        _reset = reset;
    }

    public T Get()
    {
        if (_availableOccurrences.Count == 0)
        {
            _availableOccurrences.Enqueue(_createNew());
        }
        var obj = _availableOccurrences.Dequeue();
        _reset(obj);
        return obj;
    }

    public void Return(T obj)
    {
        if (obj == null)
        {
            throw new ApplicationException("Wrong returning. The object should not be null.");
        }
        _availableOccurrences.Enqueue(obj);
    }
}