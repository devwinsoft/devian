using System;
using UnityEngine;
using Devian;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TestInputController : BaseInputController
{
    void Start()
    {
        //InputManager.Instance.Bus.Subscribe(OnInputMove);
    }

    protected override void OnInputMove(Vector2 move)
    {
        Debug.Log(move);
    }
    
}
