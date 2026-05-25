using LitJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using UnityEngine;

using GD = GlobalDefines;

public static class Utils
{
    static private int _cyclePerSecond = -1; //how many FixUpdate in 1s

    //random pool
    static private List<byte> _randomPool1 = new List<byte>();
    static private List<byte> _randomPool2 = new List<byte>();
    static private int _randomPool1Index = 0, _randomPool2Index = 0;
    static private int _randomPool1Size = 0, _randomPool2Size = 0;
    static private bool _poolCreated = false;

    //将location位置的bit置一
    public static int AddBit(int target, int location)
    {
        target = target | (1 << location);
        return target;
    }

    public static int RemoveBit(int target, int location)
    {
        target = target & ~(1 << location);
        return target;
    }

    public static bool HasBit(int target, int location)
    {
        if ((target & (1 << location)) != 0)
            return true;
        else
            return false;
    }

    public static int SetBit(int target, int location, int bit)
    {
        if (bit == 0)
            return RemoveBit(target, location);
        else if (bit == 1)
            return AddBit(target, location);
        else
            return target;
    }

    public static int SetBit(int target, int location, bool bit)
    {
        if (bit == false)
            return RemoveBit(target, location);
        else //bit == true
            return AddBit(target, location);
    }

    //from vertor2 to dirction
    public static GD.DirDef CalDirByVec(Vector2 value)
    {
        GD.DirDef dir = GD.DirDef.NULLDIR;
        if (value == Vector2.zero)
            dir = GD.DirDef.CENTER;
        else if (value.x == 1 && value.y == 0)
            dir = GD.DirDef.RDir;
        else if (value.x == 0 && value.y == 1)
            dir = GD.DirDef.UDir;
        else if (value.x == -1 && value.y == 0)
            dir = GD.DirDef.LDir;
        else if (value.x == 0 && value.y == -1)
            dir = GD.DirDef.DDir;
        else if (value.x > 0 && value.y > 0)
            dir = GD.DirDef.RUDir;
        else if (value.x > 0 && value.y < 0)
            dir = GD.DirDef.RDDir;
        else if (value.x < 0 && value.y > 0)
            dir = GD.DirDef.LUDir;
        else if (value.x < 0 && value.y < 0)
            dir = GD.DirDef.LDDir;
        return dir;
    }

    //from dirction to Vector2Int
    public static Vector2Int CalVecFromDir(GD.DirDef dir)
    {
        Vector2Int value = Vector2Int.zero;
        if (dir == GD.DirDef.CENTER)
            value = Vector2Int.zero;
        else if (dir == GD.DirDef.RDir)
            value = new Vector2Int(1, 0);
        else if (dir == GD.DirDef.UDir)
            value = new Vector2Int(0, 1);
        else if (dir == GD.DirDef.LDir)
            value = new Vector2Int(-1, 0);
        else if (dir == GD.DirDef.DDir)
            value = new Vector2Int(0, -1);
        else if (dir == GD.DirDef.RUDir)
            value = new Vector2Int(1, 1);
        else if (dir == GD.DirDef.RDDir)
            value = new Vector2Int(1, -1);
        else if (dir == GD.DirDef.LUDir)
            value = new Vector2Int(-1, 1);
        else if (dir == GD.DirDef.LDDir)
            value = new Vector2Int(-1, -1);

        return value;
    }

    static public bool IsSameDir(Vector2 vec, GD.DirDef dir)
    {
        bool ret = false;

        switch (dir)
        {
            case GD.DirDef.RDir:
                if (vec.x > 0)
                    ret = true;
                break;
            case GD.DirDef.LDir:
                if (vec.x < 0)
                    ret = true;
                break;
            case GD.DirDef.UDir:
                if (vec.y > 0)
                    ret = true;
                break;
            case GD.DirDef.DDir:
                if (vec.y < 0)
                    ret = true;
                break;
            case GD.DirDef.LUDir:
                if (vec.x < 0 && vec.y > 0)
                    ret = true;
                break;
            case GD.DirDef.RUDir:
                if (vec.x > 0 && vec.y > 0)
                    ret = true;
                break;
            case GD.DirDef.RDDir:
                if (vec.x > 0 && vec.y < 0)
                    ret = true;
                break;
            case GD.DirDef.LDDir:
                if (vec.x < 0 && vec.y < 0)
                    ret = true;
                break;
            default:
                break;
        }

        return ret;
    }

    //get the opposite direction
    static public GD.DirDef GetOppositeDir(GD.DirDef dir)
    {
        switch (dir)
        {
            case GD.DirDef.RDir:
                return GD.DirDef.LDir;
            case GD.DirDef.LDir:
                return GD.DirDef.RDir;
            case GD.DirDef.UDir:
                return GD.DirDef.DDir;
            case GD.DirDef.DDir:
                return GD.DirDef.UDir;
            case GD.DirDef.LUDir:
                return GD.DirDef.RDDir;
            case GD.DirDef.RUDir:
                return GD.DirDef.LDDir;
            case GD.DirDef.RDDir:
                return GD.DirDef.LUDir;
            case GD.DirDef.LDDir:
                return GD.DirDef.RUDir;
            default:
                return GD.DirDef.NULLDIR;
        }
    }

    public static Vector2 Degree2Vector(float degree)
    {
        return (new Vector2(Mathf.Cos(Degree2Radian(degree)), Mathf.Sin(Degree2Radian(degree))));
    }

    public static float Vector2Degree(Vector2 vec)
    {
        return AngleBetweenVector(vec, Vector2.right);
    }

    public static float Vector2Degree(Vector2 end, Vector2 start)
    {
        return AngleBetweenVector(end, start, Vector2.right);
    }

    //计算向量direction旋转到vec的角度 (-180~180),
    //direction normally is Vector2.right
    public static float AngleBetweenVector(Vector2 vec, Vector2 direction)
    {
        return Vector2.SignedAngle(direction, vec);
    }

    //计算向量direction旋转到（end-start）的角度 (-180~180)
    //direction normally is Vector2.right
    public static float AngleBetweenVector(Vector2 end, Vector2 start, Vector2 direction)
    {
        return Vector2.SignedAngle(direction, (end - start));
    }

    public static float Degree2Radian(float degree)
    {
        return (degree * Mathf.Deg2Rad);
    }

    public static float Radian2Degree(float radian)
    {
        return (radian * Mathf.Rad2Deg);
    }

    //逆时针旋转vector
    public static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        float tx = vector.x;
        float ty = vector.y;

        return new Vector2(cos * tx - sin * ty, sin * tx + cos * ty);
    }

    //calculate two vector intersection
    //vector : start at position p1, direction v1
    //vector : start at position p2, direction v2
    public static bool CalIntersection(Vector2 p1, Vector2 v1, Vector2 p2, Vector2 v2, out Vector2 intersection)
    {
        intersection = new Vector2();

        float denominator = v1.x * v2.y - v1.y * v2.x;
        // denominator can not be 0
        if (Mathf.Abs(denominator) < Mathf.Epsilon)
        {
            return false;
        }

        float t = ((p2.x - p1.x) * v2.y - (p2.y - p1.y) * v2.x) / denominator;

        intersection = p1 + t * v1;
        return true;
    }

    public static float CountOfFixUpdate(float value)
    {
        if (value >= 0)
            return value / Time.fixedDeltaTime;
        else
            return -1;
    }

    //how many FixUpdate in 1s
    public static int CountOfFixUpdateInSecond()
    {
        if (_cyclePerSecond < 0)
            _cyclePerSecond = (int)(1.0f / Time.fixedDeltaTime);
        return _cyclePerSecond;
    }

    //calculate the position move from start -> end and distance from start at len
    public static Vector2 CalPosAtDir(Vector2 start, Vector2 end, float len)
    {
        Vector2 direction = (end - start).normalized;
        return start + direction * len;
    }

    //check enum is in range exclude min and max
    //min < value < max
    public static bool IsEnumInRange<TEnum>(TEnum value, TEnum min, TEnum max) where TEnum : Enum
    {
        // convert enum to int
        int intValue = Convert.ToInt32(value);
        int intMin = Convert.ToInt32(min);
        int intMax = Convert.ToInt32(max);
        return intValue > intMin && intValue < intMax;
    }

#region xml

    public static XmlDocument LoadXmlFile(string path)
    {
        TextAsset xmlFile = Resources.Load<TextAsset>(path);
        XmlDocument confXML = new XmlDocument();
        confXML.LoadXml(xmlFile.text);
        return confXML;
    }

#endregion

#region render

    //sprite to texture2d
    public static void SpriteToTexture2D(out Texture2D to, Sprite from)
    {
        to = new Texture2D((int)from.rect.width, (int)from.rect.height, TextureFormat.RGBA32, false);
        var pixels = from.texture.GetPixels(
            (int)from.textureRect.x,
            (int)from.textureRect.y,
            (int)from.textureRect.width,
            (int)from.textureRect.height);
        to.SetPixels(pixels);
        to.Apply();
    }

    //rendert texture to texture2d
    public static void RendertextureToTexture2D(out Texture2D to, RenderTexture from)
    {
        to = new Texture2D(from.width, from.height, TextureFormat.RGBA32, false);
        Graphics.CopyTexture(from, to);
    }

    public static void Texture2DToSprite(out Sprite to, Texture2D from)
    {
        to = Sprite.Create(from, new Rect(0, 0, from.width, from.height), new Vector2(0.5f, 0.5f), GD.PixelPerUnit, 0, SpriteMeshType.FullRect,
            Vector4.zero, false);
    }

    public static void CalTimeSpend(bool isStart, string msg = null)
    {
        string strNowTime = GetCurTime();
        if (isStart)
            Debug.Log("start: " + strNowTime + " " + msg);
        else
            Debug.Log("end:   " + strNowTime + " " + msg);
    }

    public static string GetCurTime(bool year = false, bool month = false, bool day = false, bool hour = false)
    {
        DateTime dateTime = DateTime.Now;
        string nowTime = "";
        if (year == true)
            nowTime += string.Format("{0:D4}-", dateTime.Year);
        if (month == true)
            nowTime += string.Format("{0:D2}-", dateTime.Month);
        if (day == true)
            nowTime += string.Format("{0:D2}-", dateTime.Day);
        if (hour == true)
            nowTime += string.Format("{0:D2}-", dateTime.Hour);
        nowTime += string.Format("{0:D2}-{1:D2}-{2:D4}", dateTime.Minute, dateTime.Second, dateTime.Millisecond);
        return nowTime;
    }

    public static GD.DirDef CalDirDef(GD.DirDef oriValue, GD.DirDef newValue, GD.CalDeltaType type)
    {
        switch (type)
        {
            case GD.CalDeltaType.ADD:
            {
                return oriValue | newValue;
            }
            case GD.CalDeltaType.SUB:
            {
                return oriValue ^ newValue;
            }
            default:
            {
                break;
            }
        }

        return oriValue;
    }

    public static Vector2 GetNormalizedDirVec(GD.DirDef dir)
    {
        return GD.DirVector.TryGetValue(dir, out var value) ? value : Vector2.zero;
    }

    public static float CalDeltaValue(float oriValue, float delta, GD.CalDeltaType type)
    {
        switch (type)
        {
            case GD.CalDeltaType.ADD:
                return oriValue + delta;
            case GD.CalDeltaType.SUB:
                return oriValue - delta; //delta > 0
            case GD.CalDeltaType.MUL:
                return oriValue * delta; //delta > 0
            case GD.CalDeltaType.DIV:
                if (delta == 0)
                    return 0;
                return oriValue / delta; //delta > 0
            case GD.CalDeltaType.ADDSUB:
                return oriValue + delta; //delta can be nagetive
            case GD.CalDeltaType.MULDIV:
            {
                if (delta > 0)
                    return oriValue * delta;
                else if (delta < 0)
                    return oriValue / (-delta); //delta is nagetive
                else
                    return 0;
            }
                break;
            case GD.CalDeltaType.MULPERCENT:
                return oriValue * (1 + delta);
            case GD.CalDeltaType.DIVPERCENT:
                if (delta == 1)
                    return 0;
                return oriValue / (1 - delta);
            case GD.CalDeltaType.MULDIVPERCENT:
                return oriValue * (1 + delta); //delta can be nagetive
            case GD.CalDeltaType.EQUAL:
                return delta;
            default:
                return 0;
        }

        return 0;
    }

    //do calculation, the outValue must in the range (min,max) not include the min/max.
    //if not in the range return false
    public static bool CalDeltaValueInRange(float oriValue, float delta, GD.CalDeltaType type, float min, float max, ref float outValue)
    {
        float newValue = CalDeltaValue(oriValue, delta, type);
        if (newValue >= max || newValue <= min)
            return false;
        outValue = newValue;
        return true;
    }

#endregion

    //recursion get all of the child gameobject of the parent
    public static List<Transform> GetChildrenGameObjects(Transform parent)
    {
        List<Transform> openList = new List<Transform>();
        List<Transform> closeList = new List<Transform>();

        for (int i = 0; i < parent.childCount; i++)
        {
            openList.Add(parent.GetChild(i));
        }

        while (openList.Count > 0)
        {
            Transform target = openList[0];
            openList.RemoveAt(0);
            closeList.Add(target);
            int cnt = target.childCount;
            for (int i = 0; i < cnt; i++)
            {
                openList.Add(target.GetChild(i));
            }
        }

        return closeList;
    }

    //获取ValueTuple中的第一元素，默认这个地方填type
    //使用dynamic影响性能，使用反射只能返回字符串还要出来字符串也影响性能，所以选择这种蠢办法
    //约束T为值类型， T?：使得值类型也支持null
    public static T? GetTypeOfValueTuple<T>(object tuple) where T : struct
    {
        switch (tuple)
        {
            case ValueTuple<T, object> t1:
                return t1.Item1;
            case ValueTuple<T, object, object> t2:
                return t2.Item1;
            case ValueTuple<T, object, object, object> t3:
                return t3.Item1;
            case ValueTuple<T, object, object, object, object> t4:
                return t4.Item1;
            case ValueTuple<T, object, object, object, object, object> t5:
                return t5.Item1;
            case ValueTuple<T, object, object, object, object, object, object> t6:
                return t6.Item1;
            default:
                break;
        }

        return null;
    }

    //Quadratic Bezier
    public static Vector2 CalculateBezierPos(float t, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        Vector2 p = u * u * p0;
        p += 2 * u * t * p1;
        p += tt * p2;
        return p;
    }

    public static Vector2 CalculateBezierControlPos(Vector2 A, Vector2 B, float theta1, float theta2)
    {
        Vector2 AB = B - A;
        Vector2 directionAC = RotateVector(AB, theta1);
        Vector2 directionBC = RotateVector(AB, -theta2);
        Vector2 C;
        CalIntersection(A, directionAC, B, directionBC, out C);
        return C;
    }

    public static float CalculateBezierCurveLength(Vector2 startPoint, Vector2 controlPoint, Vector2 endPoint)
    {
        float length = 0f;
        Vector3 previousPoint = startPoint;

        int splitCnt = 5;
        for (int i = 1; i <= splitCnt; i++)
        {
            float tSample = i / splitCnt;
            Vector3 currentPoint = CalculateBezierPos(tSample, startPoint, controlPoint, endPoint);
            length += Vector3.Distance(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        return length;
    }

    //random int
    public static void CreateRandomPool()
    {
        if (_poolCreated == true)
            return;

        //两个pool大小互质才可达到组合最大化
        {
            //pool1
            _randomPool1Size = 4099;
            List<byte> tmpPool = new List<byte>();
            int cycleMax = _randomPool1Size / 100 + 1;
            //生成一个均匀,包含0-100所有数的原始数据
            for (int i = 0; i < cycleMax; i++)
            {
                for (byte j = 0; j < 100; j++)
                {
                    tmpPool.Add(j);
                }
            }

            for (int i = _randomPool1Size - 1; i >= 0; i--)
            {
                int index = UnityEngine.Random.Range(0, i);
                byte value = tmpPool[index];
                tmpPool.RemoveAt(index);
                _randomPool1.Add(value);
            }

            _randomPool1Index = 0;
        }

        {
            //pool2
            _randomPool2Size = 4093;
            List<byte> tmpPool = new List<byte>();
            int cycleMax = _randomPool2Size / 100 + 1;
            for (int i = 0; i < cycleMax; i++)
            {
                for (byte j = 0; j < 100; j++)
                {
                    tmpPool.Add(j);
                }
            }

            for (int i = _randomPool2Size - 1; i >= 0; i--)
            {
                int index = UnityEngine.Random.Range(0, i);
                byte value = tmpPool[index];
                tmpPool.RemoveAt(index);
                _randomPool2.Add(value);
            }

            _randomPool2Index = 0;
        }

        _poolCreated = true;
    }

    //get a 0~100 random int
    public static int GetRandomInt()
    {
        if (_randomPool1Index >= _randomPool1Size)
            _randomPool1Index = 0;
        if (_randomPool2Index >= _randomPool2Size)
            _randomPool2Index = 0;

        return (_randomPool1[_randomPool1Index++] + _randomPool2[_randomPool2Index++]) >> 1;
    }

    // UI计算父节点及所有子节点的整体包围盒（屏幕坐标）,会递归搜索所有节点
    // excludeChildren: 不计算计入包围盒的子节点的名字list
    public static Rect CalculateWorldBounds(RectTransform parent, List<string> excludeChildren = null)
    {
        Vector3[] corners = new Vector3[4];
        bool first = true;
        Vector3 min = Vector3.zero, max = Vector3.zero;

        foreach (RectTransform rt in parent.GetComponentsInChildren<RectTransform>())
        {
            if (excludeChildren != null && excludeChildren.Contains(rt.name))
                continue;

            rt.GetWorldCorners(corners);

            for (int i = 0; i < 4; i++)
            {
                Vector3 c = corners[i];
                if (first)
                {
                    min = max = c;
                    first = false;
                }
                else
                {
                    min = Vector3.Min(min, c);
                    max = Vector3.Max(max, c);
                }
            }
        }

        return new Rect(min, max - min);
    }

    //sqrt的近似计算
    public static Vector3 FastNormalized(Vector3 v)
    {
        float invMag = FastInvSqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        return new Vector3(v.x * invMag, v.y * invMag, v.z * invMag);
    }

    private static float FastInvSqrt(float x)
    {
        float xhalf = 0.5f * x;
        int i = BitConverter.ToInt32(BitConverter.GetBytes(x), 0); // float → int
        i = 0x5f3759df - (i >> 1);
        float y = BitConverter.ToSingle(BitConverter.GetBytes(i), 0); // int → float
        y = y * (1.5f - xhalf * y * y);
        return y;
    }

    //将各种字符串转成数字
    // "10%" → 0.1
    // "10" → 10
    // "0.5" → 0.5
    // "0.5%" → 0.005
    // 空字符串/非法输入 → 返回 -1
    public static float ParseStringToNumber(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0f;

        input = input.Trim();
        bool isPercent = input.EndsWith("%");
        if (isPercent)
            input = input.Substring(0, input.Length - 1);
        if (!float.TryParse(input, out float value))
            return -1f;

        return isPercent ? value / 100f : value;
    }

    //获取一个class变量的地址标识，相当于获取了一个class的指针地址，用于调试
    public static int GetClassAddrId<T>(T obj) where T : class
    {
        if (obj == null)
            return -1;

        // 获取内存级别的唯一 HashCode
        return RuntimeHelpers.GetHashCode(obj);
    }

    //List的快速删除,实现O(1)的删除
    //会打乱list的排序
    //return : true 发生了交换
    //         flase 没有交换
    public static bool ExecuteSwapAndPop<Ttype>(List<Ttype> list, int index)
    {
        int lastIndex = list.Count - 1;
        if (index < lastIndex) //不是最后一个
        {
            list[index] = list[lastIndex];
            list.RemoveAt(lastIndex);
            return true;
        }
        return false;
    }
}
