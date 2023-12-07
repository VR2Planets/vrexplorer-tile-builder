using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private readonly static Queue<Action> ExecutionQueue = new Queue<Action>();
    [CanBeNull] private static MainThreadDispatcher _instance = null;

    private void Awake()
    {
        if (_instance != null && _instance != this) Destroy(gameObject);
    }

    public void Update()
    {
        Queue<Action> tmpQueue;

        lock (ExecutionQueue)
        {
            // Create a temporary queue and move all actions to it
            tmpQueue = new Queue<Action>(ExecutionQueue);
            ExecutionQueue.Clear();
        }

        // Process the temporary queue outside the lock
        while (tmpQueue.Count > 0)
        {
            tmpQueue.Dequeue().Invoke();
        }
    }

    public static void ExecuteOnMainThread(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
        if (_instance == null)
        {
            var go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<MainThreadDispatcher>();
        }

        lock (ExecutionQueue)
        {
            ExecutionQueue.Enqueue(action);
        }
    }
}