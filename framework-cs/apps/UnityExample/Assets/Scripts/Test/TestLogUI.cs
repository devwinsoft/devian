using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class TestLogUI : MonoBehaviour
{
    public ScrollRect scrollRect;
    public TextMeshProUGUI logText;

    StringBuilder mStrBuilder = new StringBuilder();
    List<string> mLogMessages = new List<string>();


    private void Awake()
    {
        Application.logMessageReceived += LogCallback;
        logText.text = string.Empty;
        logText.OnPreRenderText += (info) =>
        {
            scrollRect.normalizedPosition = new Vector2(0, 0);
        };
    }


    private void OnDestroy()
    {
        Application.logMessageReceived -= LogCallback;
    }


    public void Clear()
    {
        mStrBuilder.Clear();
        mLogMessages.Clear();
        logText.text = string.Empty;
        LayoutRebuilder.ForceRebuildLayoutImmediate(logText.GetComponent<RectTransform>());
    }


    void LogCallback(string log, string stackTrace, LogType type)
    {
        switch (type)
        {
            case LogType.Error:
                mLogMessages.Add($"<color=red>[{DateTime.Now.ToString("HH:mm:ss")}] {log}</color>");
                break;
            case LogType.Warning:
                mLogMessages.Add($"<color=yellow>[{DateTime.Now.ToString("HH:mm:ss")}] {log}</color>");
                break;
            default:
                mLogMessages.Add($"[{DateTime.Now.ToString("HH:mm:ss")}] {log}");
                break;
        }

        if (mLogMessages.Count > 6)
        {
            mLogMessages.RemoveAt(0);
        }

        mStrBuilder.Clear();
        foreach (string msg in mLogMessages)
        {
            mStrBuilder.AppendLine(msg);
        }
        logText.text = mStrBuilder.ToString();
        LayoutRebuilder.ForceRebuildLayoutImmediate(logText.GetComponent<RectTransform>());
    }


}
