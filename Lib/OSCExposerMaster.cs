using System.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityOSC;

public class OSCExposerMaster : MonoBehaviour
{
    public static OSCExposerMaster Instance;

    public bool IsConnected;

    public bool ShowDebug;

    public string RootOSCAddress;

    private int _OSCInputPort = 6001;
    public int OSCInputPort
    {
        get
        {
            return _OSCInputPort;
        }
        set
        {
            _OSCInputPort = value;
            Connect();
        }
    }

    [HideInInspector]
    public string TempFileName = "_temp.pst";
    [HideInInspector]
    public string OSCReceiverName;

    public static Dictionary<string, ExposedObject> RegisteredObjects = new Dictionary<string, ExposedObject>();

    private List<string> _clients;

    public delegate void ExposedObjectAdded(ExposedObject controllable);
    public static event ExposedObjectAdded exposedObjectAdded;

    public delegate void ExposedObjectRemoved(ExposedObject controllable);
    public static event ExposedObjectRemoved exposedObjectRemoved;

    private void Awake()
    {
        Instance = this;
        var receiver = OSCMaster.CreateReceiver(OSCReceiverName, OSCInputPort);
        if (receiver == null)
            return;

        receiver.messageReceived += processMessage;
        IsConnected = true;
    }

    private void Connect() {
        IsConnected = false;

        if (OSCMaster.Receivers.ContainsKey(OSCReceiverName))
        {
            OSCMaster.Receivers[OSCReceiverName].messageReceived -= processMessage;
            OSCMaster.RemoveReceiver(OSCReceiverName);
        }

        var receiver = OSCMaster.CreateReceiver(OSCReceiverName, OSCInputPort);
        if (receiver == null)
            return;

        receiver.messageReceived += processMessage;

        IsConnected = true;
    }

    void processMessage(OSCMessage m)
    {
        if (!RootOSCAddress.StartsWith("/"))
            RootOSCAddress = "/" + RootOSCAddress;

        if (!RootOSCAddress.EndsWith("/"))
            RootOSCAddress += "/";

        var address = m.Address;
        if (address.StartsWith(RootOSCAddress))
        {
            address = address.Remove(0, RootOSCAddress.Length);
            string target = "";
            string property = "";

            string[] addressSplit = address.Split(new char[] { '/' });

            target = addressSplit[0];
            property = addressSplit[1];

            if (ShowDebug)
                Debug.Log("Message received for Target : " + target + ", property = " + property);

            UpdateValue(target, property, m.Data);
        }
    }

    public static void Register(ExposedObject candidate)
    {
        if (Instance.ShowDebug)
            Debug.Log("[OSCExposerMaster] Registering " + candidate.Id);

        if (!RegisteredObjects.ContainsKey(candidate.Id))
        {
            candidate.transform.parent = Instance.transform;
            RegisteredObjects.Add(candidate.Id, candidate);
            if(exposedObjectAdded != null)
                exposedObjectAdded(candidate);
        }
        else
        {
            Debug.LogWarning("[OSCExposerMaster] Candidate " + candidate.Id + " already registered");
        }
    }

    public static void Unregister(ExposedObject candidate)
    {
        if (Instance.ShowDebug)
            Debug.Log("[OSCExposerMaster] Unregistering " + candidate.Id);

        if (RegisteredObjects.ContainsKey(candidate.Id))
        {
            RegisteredObjects.Remove(candidate.Id);
            if (exposedObjectRemoved != null)
                exposedObjectRemoved(candidate);
        }
    }

    public static void UpdateValue(string target, string property, List<object> values)
    {
        if (RegisteredObjects.ContainsKey(target))
            setProp(RegisteredObjects[target], property, values);
        else
            Debug.LogWarning("[OSCExposerMaster] Target : \"" + target + "\" is unknown !");
    }

    public static void LoadEveryPresets()
    {
        foreach (var controllable in RegisteredObjects)
        {
            Instance.LoadLatestUsedPreset(controllable.Value);
        }
    }

    public static void SaveAllPresets()
    {
        foreach (var controllable in RegisteredObjects)
        {
            SaveExposedObject(controllable.Value);
        }
    }

    public static void SaveAsAllPresets()
    {
        foreach (var controllable in RegisteredObjects)
        {
            SaveExposedObjectAs(controllable.Value);
        }
    }

    public static void LoadAllPresets()
    {
        foreach (var controllable in RegisteredObjects)
        {
            Instance.LoadPreset(controllable.Value);
        }
    }

    public static void RefreshAllPresets()
    {
        foreach (var controllable in RegisteredObjects)
        {
            controllable.Value.UpdatePresetList();
        }
    }

    public static void setProp(ExposedObject exposedObject, string property, List<object> values)
    {
        ClassAttributInfo info = exposedObject.getPropInfoForAddress(property);
        if (info != null)
        {
            setFieldProp(exposedObject, info, values);
            return;
        }

        MethodInfo mInfo = exposedObject.getMethodInfoForAddress(property);
        if (mInfo != null)
        {
            setMethodProp(exposedObject, mInfo, property, values);
            return;
        }
    }

    public static void setFieldProp(ExposedObject exposedObject, ClassAttributInfo info, List<object> values, bool silent = false)
    {

        if (info.ExposeSettings.isInteractible == false)
            return;

        string typeString = info.FieldType.ToString();

        if (Instance.ShowDebug)
            Debug.Log("Setting attribut  " + info.Name + " of type " + typeString + " with " + values.Count + " value(s) // " + values[0].ToString());

        // if we detect any attribute print out the data.

        if (typeString == "System.Single")
        {
            if (values.Count >= 1) info.SetValue(exposedObject.AssociatedComponent, TypeConverter.getFloat(values[0]));
        }
        else if (typeString == "System.Boolean")
        {
            if (values.Count >= 1) info.SetValue(exposedObject.AssociatedComponent, TypeConverter.getBool(values[0]));
        }
        else if (typeString == "System.Int32")
        {
            if (values.Count >= 1) info.SetValue(exposedObject.AssociatedComponent, TypeConverter.getInt(values[0]));
        }
        else if (typeString == "UnityEngine.Vector2")
        {
            if (values.Count == 1) info.SetValue(exposedObject.AssociatedComponent, (Vector2)values[0]);
            if (values.Count >= 2) info.SetValue(exposedObject.AssociatedComponent, new Vector2(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1])));
        }
        else if (typeString == "UnityEngine.Vector3")
        {
            if (values.Count == 1) info.SetValue(exposedObject.AssociatedComponent, (Vector3)values[0]);
            if (values.Count >= 3) info.SetValue(exposedObject.AssociatedComponent, new Vector3(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2])));
        }
        else if (typeString == "UnityEngine.Color")
        {
            if (values.Count == 1) info.SetValue(exposedObject, (Color)values[0]);
            else if (values.Count >= 4) info.SetValue(exposedObject.AssociatedComponent, new Color(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2]), TypeConverter.getFloat(values[3])));
            else if (values.Count >= 3) info.SetValue(exposedObject.AssociatedComponent, new Color(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2]), 1));
        }
        else if (typeString == "System.String")
        {
            // Debug.Log("String received : " + values.ToString());
            info.SetValue(exposedObject.AssociatedComponent, values[0].ToString());
        }
    }

    public static void setMethodProp(ExposedObject exposedObject, MethodInfo info, string property, List<object> values)
    {

        object[] parameters = new object[info.GetParameters().Length];

        if (Instance.ShowDebug) Debug.Log("Set Method, num expected parameters : " + parameters.Length);

        int valueIndex = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            string typeString = info.GetParameters()[i].ParameterType.ToString();
            //Debug.Log("OSC IN Method, arg "+i+" TYPE : " + typeString + ", num values in OSC Message " + values.Count);

            if (typeString == "System.Single")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.getFloat(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.getBool(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.getInt(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "UnityEngine.Vector2")
            {
                if (values.Count >= valueIndex + 2)
                {
                    parameters[i] = new Vector2(TypeConverter.getFloat(values[valueIndex]), TypeConverter.getFloat(values[valueIndex + 1]));
                    valueIndex += 2;
                }
            }
            else if (typeString == "UnityEngine.Vector3")
            {
                if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Vector3(TypeConverter.getFloat(values[valueIndex]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]));
                    valueIndex += 3;
                }
            }
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Color(TypeConverter.getFloat(values[valueIndex + 0]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), TypeConverter.getFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
                else if (values.Count >= i + 3)
                {
                    parameters[i] = new Color(TypeConverter.getFloat(values[valueIndex + 0]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), 1);
                    valueIndex += 3;
                }

            }
            else if (typeString == "System.String")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = values[i].ToString();
                    valueIndex += 1;
                }
            }

        }

        info.Invoke(exposedObject.AssociatedComponent, parameters);
    }

    #region presetArea

    public static void SaveExposedObject(ExposedObject exposedObject)
    {
        if (string.IsNullOrEmpty(exposedObject.CurrentPreset))
        {
            SaveExposedObjectAs(exposedObject);
            return;
        }

        Save(exposedObject);
    }

    private static void Save(ExposedObject exposedObject)
    {
        var targetDirectory = exposedObject.PresetDirectory;

        if (Instance.ShowDebug)
            Debug.Log("Saving in " + targetDirectory + exposedObject.CurrentPreset + "...");

        //create file
        if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);
        var file = File.OpenWrite(targetDirectory + exposedObject.CurrentPreset);
        file.Close();

        File.WriteAllText(targetDirectory + exposedObject.CurrentPreset, JsonUtility.ToJson(exposedObject.getData()));

        if (Instance.ShowDebug)
            Debug.Log("Saved in " + targetDirectory + exposedObject.CurrentPreset);

        exposedObject.UpdatePresetList();
    }

    public static void SaveExposedObjectAs(ExposedObject exposedObject)
    {
        var date = DateTime.Today.Day + "-" + DateTime.Today.Month + "-" + DateTime.Today.Year + "_" +
                   DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second;
        var fileName = date + ".pst";

        exposedObject.CurrentPreset = fileName;

        Save(exposedObject);
    }

    public void LoadPreset(ExposedObject exposedObject)
    {
        LoadPresetWithName(exposedObject, exposedObject.CurrentPreset);
    }

    public void LoadPresetWithName(ExposedObject exposedObject, string newPreset, float duration = 0, string tweenStyle = null)
    {
        if (!newPreset.EndsWith(".pst"))
            newPreset += ".pst";

        if (Instance.ShowDebug)
            Debug.Log("Loading " + newPreset + " preset for " + exposedObject.Id + " with " + (tweenStyle == null ? " no tween " : tweenStyle));

        StreamReader file;
        try
        {
            file = new StreamReader(exposedObject.PresetDirectory + newPreset);
            ExposedObjectData cData = JsonUtility.FromJson<ExposedObjectData>(file.ReadLine());
            loadData(exposedObject, cData, duration, tweenStyle);
            file.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Error while loading preset : " + e.Message + e.StackTrace);
            return;
        }
        exposedObject.CurrentPreset = newPreset;
    }

    public void LoadLatestUsedPreset(ExposedObject exposedObject)
    {
        //Check if the temp preset containing the last used preset exists
        if (!File.Exists(exposedObject.PresetDirectory + TempFileName)) return;

        var file = new StreamReader(exposedObject.PresetDirectory + TempFileName);

        var lastPresetRead = file.ReadLine();
        file.Close();

        if (Instance.ShowDebug)
            Debug.Log("LastUsedPreset for " + exposedObject.Id + " : " + lastPresetRead);

        if (string.IsNullOrEmpty(lastPresetRead)) return;

        LoadPresetWithName(exposedObject, lastPresetRead);
    }

    public void loadData(ExposedObject exposedObject, ExposedObjectData data, float duration = 0, string tweenStyle = null)
    {
        
        if (tweenStyle != null)
        {
            tweenStyle = tweenStyle.ToLower();
            if (tweenStyle != "easeout" && tweenStyle != "easein" && tweenStyle != "easeinout" && tweenStyle != "linear")
            {
                Debug.LogWarning("Unknow tween style !");
                tweenStyle = null;
            }
        }

        int index = 0;
        foreach (string dn in data.nameList)
        {
            ClassAttributInfo info;
            if (exposedObject.Attributs.TryGetValue(dn, out info))
            {
                if (tweenStyle != null)
                {
                    var curve = new AnimationCurve();

                    if (tweenStyle == "easeinout")
                        curve = TweenCurves.Instance.EaseInOutCurve;

                    else if (tweenStyle == "easein")
                        curve = TweenCurves.Instance.EaseInCurve;

                    else if (tweenStyle == "easeout")
                        curve = TweenCurves.Instance.EaseOutCurve;

                    else if (tweenStyle == "linear")
                        curve = TweenCurves.Instance.LinearCurve;

                    StartCoroutine(
                            TweenValue(exposedObject, exposedObject.Attributs[dn],
                                TypeConverter.getObjectForValue(exposedObject.Attributs[dn].FieldType.ToString(), data.valueList[index]),
                                duration,
                                curve)
                            );
                }
                else
                {
                    List<object> values = new List<object>();
                    values.Add(TypeConverter.getObjectForValue(exposedObject.Attributs[dn].FieldType.ToString(), data.valueList[index]));
                    setFieldProp(exposedObject, exposedObject.Attributs[dn], values);
                }
            }

            index++;
        }
        //StartCoroutine(CallAfterDuration(DataLoaded, duration));
    }

    //IEnumerator CallAfterDuration(Action callback, float duration)
    //{
    //    var currentTime = 0.0f;
    //    while (currentTime < duration)
    //    {
    //        currentTime += Time.deltaTime;
    //        yield return new WaitForFixedUpdate();
    //    }

    //    callback();
    //}

    IEnumerator TweenValue(ExposedObject exposedObject, ClassAttributInfo fieldInfo, object end, float duration, AnimationCurve curve)
    {
        var currentTime = 0f;
        var startValue = fieldInfo.GetValue(this);
        while (currentTime < duration)
        {
            List<object> values = new List<object>();
            //            Debug.Log(fieldInfo.FieldType.ToString() );
            if (fieldInfo.FieldType.ToString() == "System.Single")
            {
                values.Add(Mathf.Lerp((float)startValue, (float)end, curve.Evaluate(currentTime / duration)));
            }

            else if (fieldInfo.FieldType.ToString() == "System.Int32")
            {
                values.Add((int)Mathf.Lerp((int)startValue, (int)end, curve.Evaluate(currentTime / duration)));
            }

            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Vector2")
            {
                values.Add(Vector2.Lerp((Vector2)startValue, (Vector2)end, curve.Evaluate(currentTime / duration)));
            }

            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Vector3")
            {
                values.Add(Vector3.Lerp((Vector3)startValue, (Vector3)end, curve.Evaluate(currentTime / duration)));
            }

            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Color")
            {
                values.Add(Color.Lerp((Color)startValue, (Color)end, curve.Evaluate(currentTime / duration)));
            }
            else
            {
                break;
            }
            setFieldProp(exposedObject, fieldInfo, values);
            currentTime += Time.deltaTime;

            yield return new WaitForFixedUpdate();
        }

        List<object> finalValue = new List<object>();
        finalValue.Add(end);
        setFieldProp(exposedObject, fieldInfo, finalValue);
    }
    #endregion
}