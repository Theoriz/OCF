using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class TypeConverter {

    #region Enums

    /// <summary>
    /// Resolves <paramref name="value"/> - a member name, an underlying numeric value, or an already
    /// boxed member - to a value of <paramref name="enumType"/>.
    /// </summary>
    /// <remarks>
    /// The type is taken from the FieldInfo of the member being written, so nothing here parses a
    /// type name and no assembly qualification is involved. Names go through Enum.Parse, which also
    /// accepts the comma list a [Flags] value writes itself as; numbers go through Enum.ToObject, so
    /// byte- and long-backed enums work without an int cast. A number that names no member is
    /// refused rather than stored, unless the type is [Flags] and combinations are meaningful.
    /// </remarks>
    public static bool TryGetEnumValue(Type enumType, object value, out object result)
    {
        result = null;

        if (enumType == null || !enumType.IsEnum || value == null)
            return false;

        //The UI and the undo stack both send the member itself, already boxed.
        if (enumType.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (value is string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return false;

            object parsed;
            try { parsed = Enum.Parse(enumType, text, true); }
            catch (ArgumentException) { return false; }
            catch (OverflowException) { return false; }

            //Enum.Parse also accepts a number written as text, which can name no member at all; that
            //is refused here on the same terms as a number arriving as a number.
            if (!IsFlags(enumType) && !Enum.IsDefined(enumType, parsed))
                return false;

            result = parsed;
            return true;
        }

        //OSC delivers numbers as int or float; Convert covers the rest of the numeric boxes for free.
        if (value is IConvertible && !(value is bool) && !(value is char))
        {
            long numeric;
            try { numeric = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch (Exception) { return false; }

            var candidate = Enum.ToObject(enumType, numeric);
            if (!IsFlags(enumType) && !Enum.IsDefined(enumType, candidate))
                return false;

            result = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Whether combinations of this enum's members are meaningful.</summary>
    public static bool IsFlags(Type enumType)
    {
        return enumType != null && enumType.IsDefined(typeof(FlagsAttribute), false);
    }

    /// <summary>The member names of <paramref name="enumType"/>, joined for a message.</summary>
    public static string DescribeEnumValues(Type enumType)
    {
        return enumType == null || !enumType.IsEnum ? "" : string.Join(", ", Enum.GetNames(enumType));
    }

    #endregion

    #region Scalars

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
        if (typeString == "UnityEngine.Vector4") return StringToVector4(value);
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
            float.TryParse((string)value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
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
            int.TryParse((string)value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result);

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
            int.TryParse((string)value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result);
            return result;
        }
        if (t == typeof(bool)) return (bool)value ? 1 : 0;

        return -1;
    }

    #endregion

    #region Strings to Unity types

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
        if (sArray.Length < 4)
        {
            Debug.LogWarning("[OCF] StringToColor: malformed color string '" + sColor + "'");
            return Color.black;
        }

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
        if (sArray.Length < 2)
        {
            Debug.LogWarning("[OCF] StringToVector2: malformed string '" + sVector + "'");
            return Vector2.zero;
        }

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
        if (sArray.Length < 2)
        {
            Debug.LogWarning("[OCF] StringToVector2Int: malformed string '" + sVector + "'");
            return Vector2Int.zero;
        }

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
        if (sArray.Length < 3)
        {
            Debug.LogWarning("[OCF] StringToVector3: malformed string '" + sVector + "'");
            return Vector3.zero;
        }

        Vector3 result = new Vector3(
            float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture));

        return result;
    }

    public static Vector4 StringToVector4(string sVector)
    {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")"))
        {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector4
        if (sArray.Length < 4)
        {
            Debug.LogWarning("[OCF] StringToVector4: malformed string '" + sVector + "'");
            return Vector4.zero;
        }

        Vector4 result = new Vector4(
            float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(sArray[3], System.Globalization.CultureInfo.InvariantCulture));

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
        if (sArray.Length < 3)
        {
            Debug.LogWarning("[OCF] StringToVector3Int: malformed string '" + sVector + "'");
            return Vector3Int.zero;
        }

        Vector3Int result = new Vector3Int(
            int.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture));

        return result;
    }

    #endregion
}
