using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver; //simliar to socket
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    [SerializeField]
    Transform player = null;

    void Start()
    {
        m_Driver = NetworkDriver.Create(); //creat socket

        m_Connection = default(NetworkConnection);

        //serverIP = "3.15.221.96";
        serverIP = "127.0.0.1";
        var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        m_Connection = m_Driver.Connect(endpoint); //connect to server
    }

    public void OnDestroy()
    {
        //Both NetworkDriver and NativeList allocate unmanaged memory and need to be disposed. 
        //To make sure this happens we can simply call the Dispose method when we are done with both of them.
        m_Driver.Dispose();
    }
    void Update()
    {
        //You start the same way as you did in the server by calling m_Driver.ScheduleUpdate().Complete(); and make sure that the connection worked.
        m_Driver.ScheduleUpdate().Complete();




        //Check if connection is alive
        if (!m_Connection.IsCreated)
        {
            return;
        }





        DataStreamReader stream; //Where to store data
        NetworkEvent.Type cmd; //To check what data this is

        cmd = m_Connection.PopEvent(m_Driver, out stream); //Get data from socket
        while (cmd != NetworkEvent.Type.Empty)
        {
            //This event tells you that you have received a ConnectionAccept message and you are now connected to the remote peer.
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            //If you get data from server...
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            //Lastly we just want to make sure we handle the case that a server disconnects us for some reason.
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }

    void OnConnect()
    {
        Debug.Log("We are now connected to the server");
        Debug.Log("Connection ID: " + m_Connection.InternalId.ToString());


        InvokeRepeating("SendPlayerPosition", 0, 1); //Start sending player's position to server

        //// Example to send a handshake message:
        // HandshakeMsg m = new HandshakeMsg();
        // m.player.id = m_Connection.InternalId.ToString();
        // SendToServer(JsonUtility.ToJson(m));
    }


    void OnData(DataStreamReader stream)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(bytes); //Get bytes
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray()); //convert bytes to JSON string
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg); //convert JSON to c# class

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;

            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                //Debug.Log("Player update message received!");
                Debug.Log("Got data from server, player Pos: " + puMsg.player.pos);
                break;

            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                break;

            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void OnDisconnect()
    {
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    void SendToServer(string message)
    {
        //When you establish a connection between the client and the server, 
        //you send a data. The use of the BeginSend / EndSend pattern together with the DataStreamWriter,
        //write data into the stream, and finally send it out on the network.

        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void Disconnect()
    {
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }


    void SendPlayerPosition()
    {
        //player.position

        //// Example to send a handshake message:
        //HandshakeMsg m = new HandshakeMsg();
        //m.player.id = m_Connection.InternalId.ToString();
        //SendToServer(JsonUtility.ToJson(m));

        PlayerUpdateMsg m = new PlayerUpdateMsg();
        m.player.id = m_Connection.InternalId.ToString();
        m.player.pos = player.position;

        SendToServer(JsonUtility.ToJson(m));

        //SendToServer();
    }

}