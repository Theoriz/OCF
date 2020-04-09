using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ControllableMasterControllable : Controllable {

    [Header("Global Settings")]

    [OSCProperty]
    public bool HideCursorWithGenUI;

    [Header("OSC Settings")]

    [OSCProperty(isInteractible = false)]
    public string IPAddress;
    [OSCProperty]
    public int OSCInputPort;

    [Tooltip("If connect fails, increment port and retry.")]
    [OSCProperty] public bool IncrementalConnect;

    [OSCProperty(isInteractible = false)] public bool IsConnected;


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
