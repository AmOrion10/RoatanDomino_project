using System;
using System.Collections.Generic;

using UnityEngine;

public class MainThread : MonoBehaviour
{
    public static MainThread Instance { get; private set; }
    
    private readonly Queue<Action> actionsQueue = new();

    private void Awake()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("MainThread");
        if (objs.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Update()
    {
        Action action;
        lock (actionsQueue)
        {
            if (actionsQueue.Count == 0) return;
            action = actionsQueue.Dequeue();
        }

        action();
    }

    public void AddActionToQueue(Action action)
    {
        lock (actionsQueue)
        {
            actionsQueue.Enqueue(action);
        }
    }
}
