using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityOSC;

public class ControllableMaster : MonoBehaviour
{
    public bool IsConnected;

    public bool ShowDebug;
    public string OSCReceiverName;
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

    public static Dictionary<string, Controllable> RegisteredControllables = new Dictionary<string, Controllable>();

    public delegate void ControllableAddedEvent(Controllable controllable);
    public static event ControllableAddedEvent controllableAdded;

    public delegate void ControllableRemovedEvent(Controllable controllable);
    public static event ControllableRemovedEvent controllableRemoved;

    private void Awake()
    {
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
        //if (m == null)
        //    return;
        //if (string.IsNullOrEmpty(m.Address))
        //    return;

        //if (ShowDebug)
        //    Debug.Log("Received : " + m.Address + " " + m.Data);
        string[] addressSplit = m.Address.Split(new char[] { '/' });

        string target = "";
        string property = "";

        if (addressSplit.Length <= 2)
            return;

        if (!string.IsNullOrEmpty(RootOSCAddress))
        {
            if (addressSplit[1] != RootOSCAddress)
            {
                Debug.LogWarning("Message address[1] different from " + RootOSCAddress);
                return;
            }
           
            try
            {
                target = addressSplit[2];
                property = addressSplit[3];
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error parsing OCF command ! ");
            }
        }
        else
        {
            try
            {
                target = addressSplit[1];
                property = addressSplit[2];
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error parsing OCF command ! ");
            }
        }

        //if (addressSplit.Length == 1 || addressSplit[1] != "OCF") //If length == 1 then it's not an OSC address, don't process it but propagate anyway
        //{
        //    if (messageAvailable != null)
        //        messageAvailable(m); //propagate the message
        //}
        //else //Starts with /OCF/ so it's control
        //{
            

        if (ShowDebug)
            Debug.Log("Message received for Target : " + target + ", property = " + property);

         UpdateValue(target, property, m.Data);
    }

    public static void Register(Controllable candidate)
    {
        if (!RegisteredControllables.ContainsKey(candidate.id))
        {
            RegisteredControllables.Add(candidate.id, candidate);
            if(controllableAdded != null) controllableAdded(candidate);

            //Debug.Log("Added " + candidate.id);
        }
        else
        {
            Debug.LogWarning("ControllerMaster already contains a Controllable named " + candidate.id);
        }
    }

    public static void UnRegister(Controllable candidate)
    {
        if (RegisteredControllables.ContainsKey(candidate.id))
        {
            RegisteredControllables.Remove(candidate.id);
            if (controllableRemoved != null) controllableRemoved(candidate);
        }
    }

    public static void UpdateValue(string target, string property, List<object> values)
    {
        if (RegisteredControllables.ContainsKey(target))
            RegisteredControllables[target].setProp(property, values);
        else
            Debug.LogWarning("[ControllableMaster] Target : \"" + target + "\" is unknown !");
    }

    public static void LoadEveryPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.LoadLatestUsedPreset();
        }
    }

    public static void SaveAllPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.Save();
        }
    }

    public static void SaveAsAllPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.SaveAs();
        }
    }

    public static void LoadAllPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.Load();
        }
    }

    public static void RefreshAllPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.ReadFileList();
        }
    }
}