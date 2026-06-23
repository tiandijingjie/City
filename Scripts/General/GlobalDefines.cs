using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalDefines
{
    public static string Version = "0.0.1";

    public static int PixelPerUnit = 128;
    static public float InvalidFloat = float.MinValue;
    static public int InvalidInt = int.MinValue;
    static public Vector2 InvalidVector2 = new Vector2(InvalidFloat, InvalidFloat);
    public static Vector2Int InvalidVector2Int = new Vector2Int(InvalidInt, InvalidInt);
    public static int HourOfDay = 8;
    public static float DoubleClickTimeInterval = 0.4f; //鼠标双击的最大时间间隔
    public static int MaxGPUInstances = 1023; //GPU Instancing 每次最大绘制数量, DrawMeshInstanced 硬限制

    public static readonly Dictionary<DirDef, Vector2> DirVector = new Dictionary<DirDef, Vector2>()
    {
        {DirDef.UDir,    new Vector2(0,  1) },
        {DirDef.RDir,    new Vector2(1,  0) },
        {DirDef.DDir,    new Vector2(0, -1) },
        {DirDef.LDir,    new Vector2(-1, 0) },
        {DirDef.UDDir,   new Vector2(0,  0) },
        {DirDef.LRDir,   new Vector2(0,  0) },
        {DirDef.LUDir,   new Vector2(-0.707f,  0.707f) },
        {DirDef.RUDir,   new Vector2(0.707f,   0.707f) },
        {DirDef.RDDir,   new Vector2(0.707f,  -0.707f) },
        {DirDef.LDDir,   new Vector2(-0.707f, -0.707f) },
        {DirDef.ULRDir,  new Vector2(0,  1) },
        {DirDef.DLRDir,  new Vector2(0, -1) },
        {DirDef.LUDDir,  new Vector2(-1, 0)},
        {DirDef.RUDDir,  new Vector2(1,  0)},
        {DirDef.CENTER,  new Vector2(0,  0) },
        {DirDef.NULLDIR,  InvalidVector2 },
    };

    [Flags]
    public enum DirDef
    {
        NULLDIR = 0,//no/error dir
        UDir = 1 << 0,//up
        RDir = 1 << 1,//right
        DDir = 1 << 2,//down
        LDir = 1 << 3,//left
        UDDir = UDir|DDir,//up down
        LRDir = LDir|RDir,//left right
        LUDir = LDir|UDir,//left up
        RUDir = RDir|UDir,//right up
        RDDir = RDir|DDir,//right down
        LDDir = LDir|DDir,//left down
        ULRDir = UDir|LDir|RDir,
        DLRDir = DDir|LDir|RDir,
        LUDDir = LDir|UDir|DDir,
        RUDDir = RDir|UDir|DDir,
        CENTER = UDir|DDir|LDir|RDir, //center
        INNERDir,  //inside
        OUTDir,    //outside
    }

    public enum SceneType
    {
        MIN = 0,
        CITYSCENE,
        WARFIELDSCENE,
        MAX,
    }

    public enum Mix
    {
        PURE = 0,
        MIX,
    }

    public enum PortDir  //the port direction
    {
        InPort = 0,
        OutPort,
        InOutPort,
    }

    public enum CalDeltaType
    {
        MIN = 0,
        ADD,
        SUB,
        MUL,
        DIV,
        ADDSUB,   //add or sub
        MULDIV,   //multiply or divide
        MULPERCENT,  //x=x*(1+rate)
        DIVPERCENT,  //x=x*(1-rate)
        MULDIVPERCENT,  //x=x*(1+rate)
        EQUAL, // replace the new value to the old one x=rate
        MAX,
    }

    static public DirDef GetOptDir(DirDef dir)
    {
        switch(dir)
        {
            case DirDef.UDir:
                return DirDef.DDir;
            case DirDef.RDir:
                return DirDef.LDir;
            case DirDef.DDir:
                return DirDef.UDir;
            case DirDef.LDir:
                return DirDef.RDir;
            case DirDef.LUDir:
                return DirDef.RDDir;
            case DirDef.RUDir:
                return DirDef.LDDir;
            case DirDef.RDDir:
                return DirDef.LUDir;
            case DirDef.LDDir:
                return DirDef.RUDir;
            default:
                break;
        }
        return DirDef.NULLDIR;
    }

    //the direction from -> to
    static public DirDef GetDir(Vector2Int from, Vector2Int to)
    {
        if(from.x == to.x)
        {
            if (from.y == to.y)
                return DirDef.CENTER;
            else if (from.y > to.y)
                return DirDef.DDir;
            else if(from.y < to.y)
                return DirDef.UDir;
        }
        else if(from.y == to.y)
        {
            if (from.x > to.x)
                return DirDef.LDir;
            else
                return DirDef.RDir;
        }
        else if(from.x > to.x)
        {
            if (from.y > to.y)
                return DirDef.LDDir;
            else if(from.y < to.y)
                return DirDef.LUDir;
        }
        else if(from.x < to.x)
        {
            if (from.y > to.y)
                return DirDef.RDDir;
            else if (from.y < to.y)
                return DirDef.RUDir;
        }

        return DirDef.NULLDIR;
    }

    static public DirDef GetDir(Vector2 from, Vector2 to)
    {
        if (from.x == to.x)
        {
            if (from.y == to.y)
                return DirDef.CENTER;
            else if (from.y > to.y)
                return DirDef.DDir;
            else if (from.y < to.y)
                return DirDef.UDir;
        }
        else if (from.y == to.y)
        {
            if (from.x > to.x)
                return DirDef.LDir;
            else
                return DirDef.RDir;
        }
        else if (from.x > to.x)
        {
            if (from.y > to.y)
                return DirDef.LDDir;
            else if (from.y < to.y)
                return DirDef.LUDir;
        }
        else if (from.x < to.x)
        {
            if (from.y > to.y)
                return DirDef.RDDir;
            else if (from.y < to.y)
                return DirDef.RUDir;
        }

        return DirDef.NULLDIR;
    }

    //if the dirction is the straight
    static public bool IsDirStraight(DirDef dir)
    {
        if(dir == DirDef.UDir || dir == DirDef.DDir || dir == DirDef.LDir || dir == DirDef.RDir)
            return true;
        return false;
    }

    static public Vector2 GetDirVector(DirDef dir)
    {
        Vector2 ret = Vector2.zero;

        switch(dir)
        {
            case DirDef.CENTER:
                ret = Vector2.zero;
                break;
            case DirDef.UDir:
                ret = new Vector2(0, 1);
                break;
            case DirDef.DDir:
                ret = new Vector2(0, -1);
                break;
            case DirDef.LDir:
                ret = new Vector2(-1, 0);
                break;
            case DirDef.RDir:
                ret = new Vector2(1, 0);
                break;
            case DirDef.LDDir:
                ret = new Vector2(-1, -1);
                break;
            case DirDef.LUDir:
                ret = new Vector2(-1, 1);
                break;
            case DirDef.RUDir:
                ret = new Vector2(1, 1);
                break;
            case DirDef.RDDir:
                ret = new Vector2(1, -1);
                break;
            default:
                break;
        }
        return ret;
    }
}
