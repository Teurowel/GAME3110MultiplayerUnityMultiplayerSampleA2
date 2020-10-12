/*
Data from client to server:  (ReadBytes) -> bytes -> (GetString) -> JSON string -> (FromJson) -> c# class
Data from server to client:   c# class -> (ToJson) -> JSON string -> (GetBytes) -> bytes -> (WriteBytes)
  
 */



using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
//using Unity.Collections.Generic;
//using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver; //similar to socket
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections; //To hold our connections

    //private ServerUpdateMsg listOfPlayers = new ServerUpdateMsg();
    private Dictionary<string, NetworkObjects.NetworkPlayer> listOfClients = new Dictionary<string, NetworkObjects.NetworkPlayer>(); //Dictionary for all clients

    float lastTimeSendAllPlayerInfo = 0f;
    float intervalOfSendingAllPlayerInfo = 0.03f; //every 1 seconds, send all player info to all clients

    float lastTimeChangeColorOfPlayers = 0f;
    float intervalOfChangeColorOfPlayers = 1f; //every 1 seconds change color of players

    void Start()
    {
        m_Driver = NetworkDriver.Create(); // just makes sure you are creating your driver without any parameters.
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;

        //Then we try to bind our driver to a specific network address and port, and if that does not fail, we call the Listen method.
        //Important: the call to the Listen method sets the NetworkDriver to the Listen state.This means that the NetworkDriver will now actively listen for incoming connections.
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        //Finally we create a NativeList to hold all the connections.
        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        //InvokeRepeating("SendAllPlayerInfoToClinet", 0, 1.0f);
    }

    public void OnDestroy()
    {
        //Both NetworkDriver and NativeList allocate unmanaged memory and need to be disposed. 
        //To make sure this happens we can simply call the Dispose method when we are done with both of them.
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void Update()
    {
        //As the com.unity.transport package uses the Unity C# Job System internally, 
        //the m_Driver has a ScheduleUpdate method call. Inside our Update loop you need to make sure to call the Complete method on the JobHandle that is returned, 
        //in order to know when you are ready to process any updates.
        m_Driver.ScheduleUpdate().Complete();


        //The first thing we want to do, after you have updated your m_Driver, is to handle your connections. 
        //Start by cleaning up any old stale connections from the list before processing any new ones.
        //This cleanup ensures that, when we iterate through the list to check what new events we have gotten, we dont have any old connections laying around.
        //Inside the "Clean up connections" block below, we iterate through our connection list and just simply remove any stale connections.
        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }


        //Under "Accept new connections" below, we add a connection while there are new connections to accept.
        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c != default(NetworkConnection)) //defulat(int, bool) will return 0, default(class, reference type) will return null, it's just checking null
        {
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }


        //Now we have an up-to-date connection list. You can now start querying the driver for events that might have happened since the last update.
        // Read Incoming Messages
        DataStreamReader stream; //This will be used in case any Data event was received. Then we just start looping through all our connections.
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated); //only when it's true

            //For each connection we want to call PopEventForConnection while there are more events still needing to get processed.
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream); //Get data from connection
            while (cmd != NetworkEvent.Type.Empty) //if cmd is not empty
            {
                //We are now ready to process events. Lets start with the Data event.
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i, m_Connections[i]); //If it was data
                }
                //Finally, you need to handle the disconnect case. This is pretty straight forward, 
                //if you receive a disconnect message you need to reset that connection to a default(NetworkConnection).
                //As you might remember, the next time the Update loop runs you will clean up after yourself.
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }

        //Every interval, send all info to all clients
        if (Time.time - lastTimeSendAllPlayerInfo >= intervalOfSendingAllPlayerInfo)
        {
            lastTimeSendAllPlayerInfo = Time.time;

            SendAllPlayerInfoToClinet();
            //Debug.Log("Send all player info to client");
        }

        if (Time.time - lastTimeChangeColorOfPlayers >= intervalOfChangeColorOfPlayers)
        {
            lastTimeChangeColorOfPlayers = Time.time;
            ChangeColorOfClient();
        }
    }
    void OnConnect(NetworkConnection c)
    {
        Debug.Log("Accepted a connection");

        ////////////////////Send internal id of connection to new client//////////////////
        PlayerUpdateMsg internalIdMsg = new PlayerUpdateMsg();
        internalIdMsg.cmd = Commands.PLAYER_INTERNALID;
        internalIdMsg.player.id = c.InternalId.ToString();
        Assert.IsTrue(c.IsCreated); //only when it's true
        SendToClient(JsonUtility.ToJson(internalIdMsg), c);
        ////////////////////////////////////////////////////////////////////////////



        //////////////////Send existed players info to new client/////////////////
        ServerUpdateMsg existedPlayers = new ServerUpdateMsg();
        existedPlayers.cmd = Commands.SPAWN_EXISTED_PLAYERS;

        //Copy player data to mesage
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> element in listOfClients)
        {
            existedPlayers.players.Add(element.Value);
        }

        //Send existed player info
        Assert.IsTrue(c.IsCreated); //only when it's true
        SendToClient(JsonUtility.ToJson(existedPlayers), c);
        ////////////////////////////////////////////////////////////////////////////



        ////////////////////Send new client info to existed player////////////////////
        PlayerUpdateMsg newPlayerMsg = new PlayerUpdateMsg();
        newPlayerMsg.cmd = Commands.SPAWN_NEW_PLAYER;
        newPlayerMsg.player.id = c.InternalId.ToString();

        //Send message to all client
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated); //only when it's true

            SendToClient(JsonUtility.ToJson(newPlayerMsg), m_Connections[i]);
        }
        ////////////////////////////////////////////////////////////////////////////



        //Add new connection to connection list
        m_Connections.Add(c);

        //Add to list of client
        listOfClients[c.InternalId.ToString()] = new NetworkObjects.NetworkPlayer();

        //// Example to send a handshake message:
        // HandshakeMsg m = new HandshakeMsg();
        // m.player.id = c.InternalId.ToString();
        // SendToClient(JsonUtility.ToJson(m),c);        
    }

    void OnData(DataStreamReader stream, int i, NetworkConnection client)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp); //Where to store bytes data
        stream.ReadBytes(bytes); //Get bytes data from stream
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray()); //convert bytes data to string(JSON)
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg); //convert JSON to c# object

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;

            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Got " + puMsg.player.id + " player Info");
                UpdateClientInfo(puMsg);
                //Debug.Log("Player update message received!");
                //Debug.Log("Got data from client, Player Pos: " + puMsg.player.pos);
                //TestSendBack(client, puMsg);
                break;

            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                break;

            default:
                Debug.Log("SERVER ERROR: Unrecognized message received!");
                break;
        }
    }

    void OnDisconnect(int i)
    {
        Debug.Log("Client disconnected from server");
        m_Connections[i] = default(NetworkConnection);
    }


    void SendToClient(string message, NetworkConnection c)
    {
        //To send anything with the NetworkDriver we need a instance of a DataStreamWriter.
        //A DataStreamWriter is a new type that comes with the com.unity.transport package. 
        //You get a DataStreamWriter when you start sending a message by calling BeginSend.

        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c); //One thing to note here is that we are NetworkPipeline.Null, to the BeginSend function. This way we say to the driver to use the unreliable pipeline to send our data. It is also possible to not specify a pipeline.
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
        writer.WriteBytes(bytes);

        //After you have written your data to your stream, you call the EndSend method on the driver and off it goes:
        m_Driver.EndSend(writer);
    }

    //void TestSendBack(NetworkConnection c, PlayerUpdateMsg gotFromClient)
    //{
    //    // Example to send a handshake message:
    //    PlayerUpdateMsg m = new PlayerUpdateMsg();
    //    m.player.id = c.InternalId.ToString();
    //    m.player.pos = gotFromClient.player.pos;
    //    SendToClient(JsonUtility.ToJson(m), c);
    //}

    void SendAllPlayerInfoToClinet()
    {
        ServerUpdateMsg m = new ServerUpdateMsg();

        //Copy player data to mesage
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> element in listOfClients)
        {
            m.players.Add(element.Value);
        }

        //Send message to all client
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated); //only when it's true

            SendToClient(JsonUtility.ToJson(m), m_Connections[i]);
        }
    }

    void UpdateClientInfo(PlayerUpdateMsg puMsg)
    {
        if (listOfClients.ContainsKey(puMsg.player.id))
        {
            listOfClients[puMsg.player.id].id = puMsg.player.id;
            listOfClients[puMsg.player.id].pos = puMsg.player.pos;
        }
    }

    void ChangeColorOfClient()
    {
        Debug.Log("Change color");
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> element in listOfClients)
        {
            element.Value.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        }
    }
}