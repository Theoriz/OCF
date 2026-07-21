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

    //Written by hand for the same reason the generator emits one: Controllable's reflection poll
    //returns object, so it would box OSCInputPort, IncrementalConnect and IsConnected once per frame.
    protected override void PollTargetScript()
    {
        var target = TargetScript as ControllableMaster;
        if (target == null) return;

        if (IPAddress != target.IPAddress) { IPAddress = target.IPAddress; RaiseScriptValueChanged("IPAddress"); }
        if (OSCInputPort != target.OSCInputPort) { OSCInputPort = target.OSCInputPort; RaiseScriptValueChanged("OSCInputPort"); }
        if (IncrementalConnect != target.IncrementalConnect) { IncrementalConnect = target.IncrementalConnect; RaiseScriptValueChanged("IncrementalConnect"); }
        if (IsConnected != target.IsConnected) { IsConnected = target.IsConnected; RaiseScriptValueChanged("IsConnected"); }
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

    [OSCMethod(showInUI = false)]
    public void LoadAll()
    {
        ControllableMaster.LoadAllPresets();
    }
}
