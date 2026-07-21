using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityOSC;
using System.Net;
using System.Net.Sockets;
using Mono.Zeroconf;

public class ControllableMaster : MonoBehaviour
{
    public static ControllableMaster instance;

    //Field order below is the inspector layout; Unity serializes by name, so grouping them costs
    //nothing. ControllableMasterEditor draws the Status pair disabled.
    [Header("OSC")]
    public string OSCReceiverName;
    public string RootOSCAddress;

    [Tooltip("If the input port is busy, increment it and retry.")]
    public bool IncrementalConnect = true;
    public int maxConnectAttempts = 100;
    private int _connectAttempts;

    [Header("Status")]
    public bool IsConnected;
    public string IPAddress;

    [Header("Presets")]
    [Tooltip("Keep presets under Documents/<product name>/Presets instead of next to the application.")]
    public bool useDocumentsDirectory = false;

    [Tooltip("Absolute path to keep presets in. Overrides Use Documents Directory. Relative paths are rejected. The " + PresetsPathArgument + " command-line argument wins over this.")]
    public string customPresetDirectory = "";

    [Header("Debug")]
    public bool ShowDebug;

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

    private RegisterService service;
    private bool zeroconfServiceCreated = false;

	private void Awake() {

        instance = this;
	}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RegisteredControllables.Clear();
        instance = null;
        _presetRootDirectory = null;
    }

    #region Preset root

    public const string PresetsPathArgument = "-presetsPath";

    private static string _presetRootDirectory;

    /// <summary>
    /// Folder every Controllable keeps its presets under, as "…/", resolved once per run.
    /// </summary>
    /// <remarks>
    /// Resolved lazily rather than in Awake: controllables read their presets from their own
    /// OnEnable, which is not guaranteed to run after this component's Awake, so <see cref="instance"/>
    /// may not be set yet when the first one asks.
    /// </remarks>
    public static string PresetRootDirectory
    {
        get
        {
            if (_presetRootDirectory != null)
                return _presetRootDirectory;

            ControllableMaster master = instance != null ? instance : FindAnyObjectByType<ControllableMaster>();
            string inspectorPath = master != null ? master.customPresetDirectory : "";
            bool useDocuments = master != null && master.useDocumentsDirectory;

            string root = ResolvePresetRoot(Environment.GetCommandLineArgs(), inspectorPath, useDocuments);

            //Checked here, once, so the unguarded Directory.CreateDirectory calls in Controllable's
            //ReadFileList and Save can never throw out of OnEnable and leave it half registered.
            string error;
            if (!TryPrepareDirectory(root, out error))
            {
                string fallback = ResolvePresetRoot(null, "", useDocuments);
                Debug.LogError("[OCF] Cannot use presets folder '" + root + "' (" + error + "). Falling back to '" + fallback + "'.");
                root = fallback;
            }

            _presetRootDirectory = root;
            return _presetRootDirectory;
        }
    }

    /// <summary>
    /// Drops the cached root so the next read resolves again. Needed in the Editor, where
    /// <see cref="customPresetDirectory"/> can be edited without entering Play - the cache is
    /// otherwise only cleared by <see cref="ResetStatics"/>.
    /// </summary>
    public static void InvalidatePresetRoot()
    {
        _presetRootDirectory = null;
    }

    /// <summary>Reveals the presets root in the system file browser.</summary>
    public void OpenPresetsFolder()
    {
        OpenFolder(PresetRootDirectory);
    }

    /// <summary>
    /// Opens a folder in the system file browser. Unlike explorer.exe, this works on Windows, macOS
    /// and Linux.
    /// </summary>
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        Application.OpenURL("file://" + path);
    }

    /// <summary>
    /// Picks the presets root: the <see cref="PresetsPathArgument"/> command-line argument first, then
    /// the inspector field, then the built-in location. Pure - no file system access.
    /// </summary>
    public static string ResolvePresetRoot(string[] commandLineArgs, string inspectorPath, bool useDocuments)
    {
        string custom = ReadPresetsPathArgument(commandLineArgs);
        string source = PresetsPathArgument;

        if (string.IsNullOrEmpty(custom))
        {
            custom = inspectorPath;
            source = "customPresetDirectory";
        }

        if (!string.IsNullOrEmpty(custom))
        {
            custom = custom.Trim();

            //A relative path would resolve against the working directory, which is not the executable
            //folder when launched from a shortcut - so it is rejected rather than quietly guessed at.
            if (Path.IsPathRooted(custom))
                return WithTrailingSlash(custom);

            Debug.LogError("[OCF] Presets path '" + custom + "' from " + source
                + " is not an absolute path and was ignored. Using the default presets folder.");
        }

        return WithTrailingSlash(DefaultPresetRoot(useDocuments));
    }

    private static string DefaultPresetRoot(bool useDocuments)
    {
        if (useDocuments)
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/" + Application.productName + "/Presets";

        return Application.dataPath + "/../Presets";
    }

    //The argument value arrives already unquoted, so a path containing spaces is one element.
    //A trailing PresetsPathArgument with nothing after it is ignored.
    private static string ReadPresetsPathArgument(string[] commandLineArgs)
    {
        if (commandLineArgs == null)
            return "";

        for (int i = 0; i < commandLineArgs.Length - 1; i++)
        {
            if (string.Equals(commandLineArgs[i], PresetsPathArgument, StringComparison.OrdinalIgnoreCase))
                return commandLineArgs[i + 1];
        }

        return "";
    }

    //Every consumer builds file paths as targetDirectory + fileName, so the trailing slash is part of
    //the contract, not cosmetic.
    private static string WithTrailingSlash(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/') + "/";
    }

    private static bool TryPrepareDirectory(string path, out string error)
    {
        try
        {
            Directory.CreateDirectory(path);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    #endregion

	private void Start()
    {
        IPAddress = GetLocalIPAddress();

        Connect();
    }

    private void Update()
    {
        if (!IsConnected && IncrementalConnect)
        {
            if (_connectAttempts < maxConnectAttempts)
            {
                _connectAttempts++;
                OSCInputPort++; // setter calls Connect()
                if (_connectAttempts >= maxConnectAttempts)
                    Debug.LogWarning("[OCF] ControllableMaster could not open an OSC input port after "
                        + maxConnectAttempts + " attempts (last tried " + OSCInputPort + "). Giving up.");
            }
        }
        else if (IsConnected)
        {
            _connectAttempts = 0;
        }
    }

    public void RefreshIP()
    {
        IPAddress = GetLocalIPAddress();
    }

    private void OnDisable() {

        if (OSCMaster.HasReceiver(OSCReceiverName))
        {
            OSCMaster.Receivers[OSCReceiverName].messageReceived -= processMessage;
            OSCMaster.RemoveReceiver(OSCReceiverName);
        }

        CloseZeroconfService();
    }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                string IPAddress = ip.ToString();
                Debug.Log("[OCF] Using local IP address "+IPAddress+".");
                return IPAddress;
            }
        }
        return "Not connected.";
    }


    private void Connect() {
        IsConnected = false;

        if (OSCMaster.HasReceiver(OSCReceiverName))
        {
            OSCMaster.Receivers[OSCReceiverName].messageReceived -= processMessage;
            OSCMaster.RemoveReceiver(OSCReceiverName);
        }

		OSCMaster.CreateReceiver(OSCReceiverName, OSCInputPort);

		if (!OSCMaster.HasReceiver(OSCReceiverName))
			return;

		OSCMaster.Receivers[OSCReceiverName].messageReceived += processMessage;

		IsConnected = true;

        if (zeroconfServiceCreated)
            CloseZeroconfService();

        CreateZeroconfService();
    }

    void processMessage(OSCMessage m)
    {
        if (m == null || string.IsNullOrEmpty(m.Address))
            return;

        string[] addressSplit = m.Address.Split(new char[] { '/' });

        string target = "";
        string property = "";

        if (addressSplit.Length <= 2)
            return;

        if (!string.IsNullOrEmpty(RootOSCAddress))
        {
            if (addressSplit.Length < 4)
                return;

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
                Debug.LogWarning("Error parsing OCF command ! " + e.Message);
            }
        }
        else
        {
            if (addressSplit.Length < 3)
                return;

            try
            {
                target = addressSplit[1];
                property = addressSplit[2];
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error parsing OCF command ! " + e.Message);
            }
        }

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

    private void CreateZeroconfService() {

        //_osc._udp.
        service = new RegisterService();
        service.Name = Application.productName + " OCF";
        service.RegType = "_osc._udp";
        service.ReplyDomain = "local.";
        service.UPort = (ushort)OSCInputPort;

        service.Register();

        zeroconfServiceCreated = true;
    }

    private void CloseZeroconfService() {

        if (!zeroconfServiceCreated)
            return;

        service.Dispose();
        zeroconfServiceCreated = false;
    }
}