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
