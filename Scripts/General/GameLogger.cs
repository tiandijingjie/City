using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;

public class GameLogger : MonoBehaviour
{
#region public parameters

    static public GameLogger Instance = null;

    public enum LogLevels
    {
        INFO = 0,
        TRACE,
        DEBUG,
        WARRNING,
        ERROR,
        EXCEPTION, //系统异常
    }

    [SerializeField] private LogLevels _curLev;
    [SerializeField] private List<string> _filterFileName = new List<string>();

    private static LogLevels curLev;
    private static List<string> filterFileName;

#endregion

#region private parameters

#endregion

#region Unity callbacks

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(this);
            return;
        }

        curLev = _curLev;
        filterFileName = _filterFileName;
    }

    //change _curLev in Inspector
    private void OnValidate()
    {
        curLev = _curLev;
        filterFileName = _filterFileName;
    }

#endregion

#region public functions

    public void SetLogLevel(LogLevels value)
    {
        _curLev = value;
        curLev = _curLev;
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public static void LogInfo(string str,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string callerClass = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
        string formattedMessage = $"[{callerClass}.{callerName} (Line {callerLineNumber}) Info]: {str}";
        PrintLog(formattedMessage, callerClass, LogLevels.INFO);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public static void LogTrace(string str,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string callerClass = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
        string formattedMessage = $"[{callerClass}.{callerName} (Line {callerLineNumber}) Trace]: {str}";
        PrintLog(formattedMessage, callerClass, LogLevels.TRACE);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public static void LogDebug(string str,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string callerClass = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
        string formattedMessage = $"[{callerClass}.{callerName} (Line {callerLineNumber}) Trace]: {str}";
        PrintLog(formattedMessage, callerClass, LogLevels.DEBUG);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public static void LogWarning(string str,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string callerClass = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
        string formattedMessage = $"[{callerClass}.{callerName} (Line {callerLineNumber}) Warning]: {str}";
        PrintLog(formattedMessage, callerClass, LogLevels.WARRNING);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public static void LogError(string str,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string callerClass = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
        string formattedMessage = $"[{callerClass}.{callerName} (Line {callerLineNumber}) Error]: {str}";
        PrintLog(formattedMessage, callerClass, LogLevels.ERROR);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public static void LogException(string str, Exception exception,  //系统异常
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string callerClass = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
        string formattedMessage = $"[{callerClass}.{callerName}:{callerLineNumber}] {str}  ===>Exception info in the next line===>";
        PrintLog(formattedMessage, callerClass, LogLevels.EXCEPTION);
        Debug.LogException(exception);
    }

#endregion

#region private functions
    // ReSharper disable Unity.PerformanceAnalysis
    [Conditional("ENABLE_PRINT")]
    private static void PrintLog(string str, string className, LogLevels lev)
    {
        if (lev < curLev)
            return;

        if (filterFileName != null && filterFileName.Count > 0)
        {
            if (filterFileName.Contains(className) == false)
                return;
        }

        string color = "";
        switch (lev)
        {
            case LogLevels.INFO:
                color = "";
                break;
            case LogLevels.TRACE:
                color = "blue";
                break;
            case LogLevels.DEBUG:
                color = "green";
                break;
            case LogLevels.WARRNING:
                color = "yellow";
                break;
            case LogLevels.ERROR:
                color = "red";
                break;
            case LogLevels.EXCEPTION:
                color = "magenta";
                break;
        }

        if (string.IsNullOrEmpty(color))
            UnityEngine.Debug.Log(str);
        else
            UnityEngine.Debug.Log($"<color={color}>{str}</color>");
    }

#endregion
}
