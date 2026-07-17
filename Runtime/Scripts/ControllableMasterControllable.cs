using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ControllableMasterControllable : Controllable {

    [Header("OSC Settings")]

    [OSCProperty(readOnly = true)]
    public string IPAddress;
    [OSCProperty]
    public int OSCInputPort;

    [Tooltip("If connect fails, increment port and retry.")]
    [OSCProperty] public bool IncrementalConnect;

    [OSCProperty(readOnly = true)] public bool IsConnected;


    [OSCMethod]
    public void RefreshIP()
    {
        (TargetScript as ControllableMaster).RefreshIP();
    }

    //The global preset methods below, by name. These exist only on this class, so matching a button
    //by name alone is not enough to identify one — a target script may legitimately expose its own
    //SaveAll. Callers must also check the controllable is a ControllableMasterControllable.
    public static readonly string[] AllPresetMethodNames = { "SaveAll", "SaveAsAll", "LoadAll" };

    [OSCMethod]
    public void SaveAll()
    {
        ControllableMaster.SaveAllPresets();
    }

    [OSCMethod]
    public void SaveAsAll()
    {
        ControllableMaster.SaveAsAllPresets();
    }

    [OSCMethod]
    public void LoadAll()
    {
        ControllableMaster.LoadAllPresets();
    }
}
