using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class TypeConverter : MonoBehaviour {

    public static int getIndexInEnum(List<string> enumValueList, string selectedElement)
    {
        var activeElementIndex = -1;
        for (var i = 0; i < enumValueList.Count; i++)
        {
            if (selectedElement == enumValueList[i].ToString())
                activeElementIndex = i;
        }

        return activeElementIndex;
    }

    public static object getObjectForValue(string typeString, string value)
    {
        if (typeString == "System.Single") return getFloat(value);
        if (typeString == "System.Boolean") return getBool(value);
        if (typeString == "System.Int32") return getInt(value);
        if (typeString == "UnityEngine.Vector2") return StringToVector2(value);
        if (typeString == "UnityEngine.Vector2Int") return StringToVector2Int(value);
        if (typeString == "UnityEngine.Vector3") return StringToVector3(value);
        if (typeString == "UnityEngine.Vector3Int") return StringToVector3Int(value);
        if (typeString == "UnityEngine.Color") return StringToColor(value);
        if (typeString == "System.String") return value;

        return null;
    }

    public static float getFloat(object value)
    {
        Type t = value.GetType();
        if (t == typeof(float)) return (float)value;
        if (t == typeof(int)) return (float)((int)value);
        if (t == typeof(string))
        {
            float result = 0;
            float.TryParse((string)value, out result);
            return result;
        }

        if (t == typeof(bool)) return (bool)value ? 1 : 0;

        return float.NaN;
    }

    public static bool getBool(object value)
    {
        Type t = value.GetType();
        if (t == typeof(float)) return (float)value >= 1;
        if (t == typeof(int)) return (int)value >= 1;
        if (t == typeof(string))
        {
            string s = ((string)value).ToLower();
            if (s == "true" || s == "1")
                return true;

            if (s == "false" || s == "0")
                return false;

            int result = 0;
            int.TryParse((string)value, out result);

            return result >= 1;
        }
        if (t == typeof(bool)) return (bool)value;

        return false;
    }
    public static int getInt(object value)
    {
        Type t = value.GetType();
        if (t == typeof(float)) return (int)((float)value);
        if (t == typeof(int)) return (int)value;
        if (t == typeof(string))
        {
            int result = 0;
            int.TryParse((string)value, out result);
            return result;
        }
        if (t == typeof(bool)) return (bool)value ? 1 : 0;

        return -1;
    }

    public static Color StringToColor(string sColor)
    {
        // Remove the parentheses
        if (sColor.StartsWith("RGBA(") && sColor.EndsWith(")"))
        {
            sColor = sColor.Substring(5, sColor.Length - 6);
        }

        // split the items
        string[] sArray = sColor.Split(',');

        // store as a Vector3
        Color result = new Color(
            float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[3], System.Globalization.CultureInfo.InvariantCulture)
        );


        return result;
    }

    public static Vector2 StringToVector2(string sVector)
    {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")"))
        {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3
        Vector2 result = new Vector2(
            float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture)
            );

        return result;
    }

    public static Vector2Int StringToVector2Int(string sVector) {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")")) {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector2Int
        Vector2Int result = new Vector2Int(
            int.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture)
            );

        return result;
    }

    public static Vector3 StringToVector3(string sVector)
    {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")"))
        {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3
        Vector3 result = new Vector3(
            float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture));

        return result;
    }

    public static Vector3Int StringToVector3Int(string sVector) {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")")) {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3Int
        Vector3Int result = new Vector3Int(
            int.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture));

        return result;
    }
}
