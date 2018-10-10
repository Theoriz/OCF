using System;
using UnityEngine;
using System.Collections.Generic;
using UnityOSC;


public class OSCMaster : MonoBehaviour
{
    public static OSCMaster instance;

    OSCReciever server;

    public int localPort = 6000;

    public String remoteHost = "127.0.0.1";
    public int remotePort = 6001;
    

    public bool isConnected;

    public bool logIncoming;
    public bool logOutgoing;

    OSCClient client;
   
    Controllable[] controllables;

    //public delegate void ValueUpdateReadyEvent(string target, string property, List<object> objects);
    //public event ValueUpdateReadyEvent valueUpdateReady;

    public delegate void MessageAvailable(OSCMessage message);
    public static event MessageAvailable messageAvailable;

    // Use this for initialization
    void Awake()
    {
        instance = this;
        client = new OSCClient(System.Net.IPAddress.Loopback, 0);
        Connect();
    }

    private void Update()
    {
        if (server == null)
            return;

        while (server.hasWaitingMessages())
            processMessage(server.getNextMessage());
    }

    public void Connect()
    {
        Debug.Log("[OCF] Connecting to port " + localPort);
        try
        {
            if(server != null)
                server.Close();

            server = new OSCReciever();
            server.Open(localPort);
           // server.PacketReceivedEvent += packetReceived;
        
            isConnected = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Error with port " + localPort);
            Debug.LogWarning(e.StackTrace);
            isConnected = false;
            server = null;
        }
    }

    //void packetReceived(OSCServer server, OSCPacket p)
    //{
    //    if (logIncoming)
    //        Debug.Log("Received : " + p.Address + p.Data + " from : " + server.LocalPort);

    //    if (p.IsBundle())
    //    {
    //        foreach (OSCMessage m in p.Data)
    //        {
    //            processMessage(m);
    //        }
    //    }else processMessage((OSCMessage)p);
    //   // Debug.Log("Packet processed");
    //}

    void processMessage(OSCMessage m)
    {   
        if(logIncoming)
               Debug.Log("Received : " + m.Address + " " + m.Data);

       string[] addressSplit = m.Address.Split(new char[] { '/' });

        if (addressSplit.Length == 1 || addressSplit[1] != "OCF") //If length == 1 then it's not an OSC address, don't process it but propagate anyway
        {
			if (messageAvailable != null)
                messageAvailable(m); //propagate the message
        }
        else //Starts with /OCF/ so it's control
        {
			string target = "";
			string property = "";
			try {
				target = addressSplit[2];
				property = addressSplit[3];
			}
			catch(Exception e) {
				Debug.LogWarning("Error parsing OCF command ! "+e.Message);
			}

			if (logIncoming) Debug.Log("Message received for Target : " + target + ", property = " + property);

            ControllableMaster.UpdateValue(target, property, m.Data);
        }
    }

    public static void sendMessage(OSCMessage m, string host, int port)
    {
        if (instance.logOutgoing)
        {
            string args = "";
            for (int i = 0; i < m.Data.Count; i++) args += (i > 0 ? ", " : "") + m.Data[i].ToString();
            Debug.Log("Sending " + m.Address + " : "+args);
        }

        instance.client.SendTo(m, host, port);
    }

    public static void sendMessage(OSCMessage m)
    {
        sendMessage(m, instance.remoteHost, instance.remotePort);
    }

    void OnApplicationQuit()
    {
        if(server != null) server.Close();
    }
}
