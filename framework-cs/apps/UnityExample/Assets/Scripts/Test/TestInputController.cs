using System;
using UnityEngine;
using Devian;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TestInputController : BaseInputController
{
    protected override void onInputMove(Vector2 move)
    {
        Debug.Log(move);
    }
    
}
