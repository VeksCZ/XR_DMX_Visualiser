using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Art-Net přijímač: poslouchá UDP 6454, ukládá ArtDmx data podle universe
// a odpovídá na ArtPoll, aby se aplikace v SoundSwitchi ukázala jako Art-Net zařízení.
//
// Na stejném PC drží port 6454 i SoundSwitch (0.0.0.0:6454). Proto se navazujeme
// přímo na IP adresy počítače – konkrétní adresa má ve Windows přednost před 0.0.0.0,
// takže unicast data pro visualizér dostaneme my, ne SoundSwitch.
public class ArtNetReceiver : MonoBehaviour
{
    public string nodeName = "DJ Visualizer";
    public int port = 6454;

    [Header("Stav (jen pro čtení)")]
    public int packetsReceived;
    public int pollsAnswered;
    public float lastPacketTime = -999f;
    public string lastSender;
    public string bindInfo;

    readonly byte[][] universes = new byte[16][];
    readonly object lockObj = new object();
    readonly List<Listener> listeners = new List<Listener>();
    volatile bool running;
    int packetCounter, pollCounter;
    volatile bool gotPacket;
    string senderStr;

    static readonly byte[] Header = Encoding.ASCII.GetBytes("Art-Net\0");

    class Listener
    {
        public UdpClient udp;
        public IPAddress ip;
        public Thread thread;
    }

    public bool HasData => Time.time - lastPacketTime < 2f;

    void OnEnable()
    {
        for (int i = 0; i < universes.Length; i++) universes[i] = new byte[512];
        running = true;

        var bound = new List<string>();
        foreach (var ip in LocalIPv4())
            if (TryListen(ip)) bound.Add(ip.ToString());
        if (bound.Count == 0 && TryListen(IPAddress.Any)) bound.Add("0.0.0.0");

        bindInfo = bound.Count > 0 ? string.Join(", ", bound) + " : " + port : "port " + port + " nejde otevřít";
        Debug.Log("Art-Net: poslouchám na " + bindInfo);
    }

    bool TryListen(IPAddress ip)
    {
        try
        {
            var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(ip, port));
            udp.EnableBroadcast = true;
            var l = new Listener { udp = udp, ip = ip };
            l.thread = new Thread(() => Loop(l)) { IsBackground = true, Name = "ArtNet " + ip };
            listeners.Add(l);
            l.thread.Start();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Art-Net: " + ip + ":" + port + " – " + e.Message);
            return false;
        }
    }

    void OnDisable()
    {
        running = false;
        foreach (var l in listeners) { try { l.udp.Close(); } catch { } }
        listeners.Clear();
    }

    void Update()
    {
        if (gotPacket)
        {
            gotPacket = false;
            lastPacketTime = Time.time;
            lastSender = senderStr;
        }
        packetsReceived = packetCounter;
        pollsAnswered = pollCounter;
    }

    // Kopie aktuálních dat universe (0-15).
    public bool GetUniverse(int universe, byte[] dest)
    {
        if (universe < 0 || universe >= universes.Length) return false;
        lock (lockObj) Buffer.BlockCopy(universes[universe], 0, dest, 0, 512);
        return true;
    }

    void Loop(Listener l)
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            byte[] p;
            try { p = l.udp.Receive(ref ep); }
            catch { if (!running) break; continue; }
            if (p.Length < 12 || !StartsWithHeader(p)) continue;
            int op = p[8] | (p[9] << 8);

            if (op == 0x5000 && p.Length >= 18) // ArtDmx
            {
                int uni = p[14] & 0x0F;
                int len = (p[16] << 8) | p[17];
                len = Math.Min(Math.Min(len, 512), p.Length - 18);
                lock (lockObj) Buffer.BlockCopy(p, 18, universes[uni], 0, len);
                Interlocked.Increment(ref packetCounter);
                senderStr = ep.Address.ToString();
                gotPacket = true;
            }
            else if (op == 0x2000) // ArtPoll
            {
                try
                {
                    var myIp = l.ip.Equals(IPAddress.Any) ? DefaultIP() : l.ip;
                    var reply = BuildPollReply(myIp.GetAddressBytes());
                    l.udp.Send(reply, reply.Length, new IPEndPoint(ep.Address, port));
                    l.udp.Send(reply, reply.Length, new IPEndPoint(IPAddress.Broadcast, port));
                    Interlocked.Increment(ref pollCounter);
                }
                catch { }
            }
        }
    }

    static bool StartsWithHeader(byte[] p)
    {
        for (int i = 0; i < 8; i++) if (p[i] != Header[i]) return false;
        return true;
    }

    byte[] BuildPollReply(byte[] ip)
    {
        var r = new byte[239];
        Buffer.BlockCopy(Header, 0, r, 0, 8);
        r[8] = 0x00; r[9] = 0x21;                       // OpPollReply
        Buffer.BlockCopy(ip, 0, r, 10, 4);
        r[14] = 0x36; r[15] = 0x19;                     // port 6454 (LE)
        r[16] = 0; r[17] = 1;
        r[20] = 0xFF; r[21] = 0xFF;                     // OEM
        r[23] = 0xD0;                                   // Status1
        WriteAscii(r, 26, nodeName, 17);
        WriteAscii(r, 44, nodeName + " (Unity)", 63);
        WriteAscii(r, 108, "#0001 [0000] OK", 63);
        r[172] = 0; r[173] = 4;                         // 4 porty
        for (int i = 0; i < 4; i++)
        {
            r[174 + i] = 0x80;                          // výstup DMX = přijímá Art-Net
            r[182 + i] = 0x80;
            r[190 + i] = (byte)i;                       // universe 0-3
        }
        r[200] = 0x00;                                  // StNode
        Buffer.BlockCopy(ip, 0, r, 207, 4);
        r[211] = 1;
        r[212] = 0x08;
        return r;
    }

    static void WriteAscii(byte[] dst, int offset, string s, int max)
    {
        var b = Encoding.ASCII.GetBytes(s);
        Buffer.BlockCopy(b, 0, dst, offset, Math.Min(b.Length, max));
    }

    static List<IPAddress> LocalIPv4()
    {
        var list = new List<IPAddress>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !list.Contains(ua.Address))
                        list.Add(ua.Address);
            }
        }
        catch (Exception e) { Debug.LogWarning("Art-Net: seznam IP nejde zjistit – " + e.Message); }
        return list;
    }

    static IPAddress DefaultIP()
    {
        try
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                s.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)s.LocalEndPoint).Address;
            }
        }
        catch { return IPAddress.Loopback; }
    }
}
