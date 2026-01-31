using UnityEngine;
using Devian;

public class TestPoolObject : MonoBehaviour, IPoolable<TestPoolObject>
{
    public AnimSequencePlayer player;

    public void OnPoolSpawned()
    {
        player.PlayDefault();
    }

    public void OnPoolDespawned()
    {
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
