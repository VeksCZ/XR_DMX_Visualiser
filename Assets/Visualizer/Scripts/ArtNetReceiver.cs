using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Art-Net přijímač: poslouchá UDP 6454, ukládá ArtDmx data podle universe
// a odpovídá na ArtPoll, aby se aplikace v SoundSwitchi ukázala jako Art-Net zařízení.
public class ArtNetReceiver : MonoBehaviour
{
    public string nodeName = "DJ Visualizer";
    public int port = 6454;

    [Header("Stav (jen pro čtení)")]
    public int packetsReceived;
    public float lastPacketTime = -999f;
    public string lastSender;

    readonly byte[][] universes = new byte[16][];
    readonly object lockObj = new object();
    UdpClient udp;
    Thread thread;
    volatile bool running;
    int packetCounter;
    volatile bool gotPacket;
    string senderStr;

    static readonly byte[] Header = Encoding.ASCII.GetBytes("Art-Net\0");

    public bool HasData => Time.time - lastPacketTime < 2f;

    void OnEnable()
    {
        for (int i = 0; i < universes.Length; i++) universes[i] = new byte[512];
        try
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            udp.EnableBroadcast = true;
            running = true;
            thread = new Thread(Loop) { IsBackground = true, Name = "ArtNet" };
            thread.Start();
            Debug.Log("Art-Net: poslouchám na UDP " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("Art-Net: nelze otevřít port " + port + " (" + e.Message + ")");
        }
    }

    void OnDisable()
    {
        running = false;
        try { udp?.Close(); } catch { }
        udp = null;
    }

    void Update()
    {
        if (gotPacket)
        {
            gotPacket = false;
            lastPacketTime = Time.time;
            packetsReceived = packetCounter;
            lastSender = senderStr;
        }
    }

    // Kopie aktuálních dat universe (0-15). Vrací false, pokud universe neexistuje.
    public bool GetUniverse(int universe, byte[] dest)
    {
        if (universe < 0 || universe >= universes.Length) return false;
        lock (lockObj) Buffer.BlockCopy(universes[universe], 0, dest, 0, 512);
        return true;
    }

    void Loop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            byte[] p;
            try { p = udp.Receive(ref ep); }
            catch { if (!running) break; continue; }
            if (p.Length < 12 || !StartsWithHeader(p)) continue;
            int op = p[8] | (p[9] << 8);

            if (op == 0x5000 && p.Length >= 18) // ArtDmx
            {
                int uni = p[14] & 0x0F;          // SubUni: subnet<<4 | universe
                int len = (p[16] << 8) | p[17];
                len = Math.Min(Math.Min(len, 512), p.Length - 18);
                lock (lockObj) Buffer.BlockCopy(p, 18, universes[uni], 0, len);
                packetCounter++;
                senderStr = ep.Address.ToString();
                gotPacket = true;
            }
            else if (op == 0x2000) // ArtPoll
            {
                try
                {
                    var reply = BuildPollReply();
                    udp.Send(reply, reply.Length, new IPEndPoint(ep.Address, port));
                    udp.Send(reply, reply.Length, new IPEndPoint(IPAddress.Broadcast, port));
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

    byte[] BuildPollReply()
    {
        var r = new byte[239];
        Buffer.BlockCopy(Header, 0, r, 0, 8);
        r[8] = 0x00; r[9] = 0x21;                       // OpPollReply
        var ip = LocalIP();
        Buffer.BlockCopy(ip, 0, r, 10, 4);
        r[14] = 0x36; r[15] = 0x19;                     // port 6454 (LE)
        r[16] = 0; r[17] = 1;                           // verze firmware
        r[18] = 0; r[19] = 0;                           // Net, Sub-Net
        r[20] = 0xFF; r[21] = 0xFF;                     // OEM
        r[23] = 0xD0;                                   // Status1
        WriteAscii(r, 26, nodeName, 17);                // Short name
        WriteAscii(r, 44, nodeName + " (Unity)", 63);   // Long name
        WriteAscii(r, 108, "#0001 [0000] OK", 63);      // Node report
        r[172] = 0; r[173] = 4;                         // 4 porty
        for (int i = 0; i < 4; i++)
        {
            r[174 + i] = 0x80;                          // umí výstup DMX = přijímá Art-Net
            r[182 + i] = 0x80;                          // GoodOutput: data se posílají
            r[190 + i] = (byte)i;                       // SwOut: universe 0-3
        }
        r[200] = 0x00;                                  // Style: StNode
        Buffer.BlockCopy(ip, 0, r, 207, 4);             // BindIp
        r[211] = 1;                                     // BindIndex
        r[212] = 0x08;                                  // Status2: umí 15-bit adresy
        return r;
    }

    static void WriteAscii(byte[] dst, int offset, string s, int max)
    {
        var b = Encoding.ASCII.GetBytes(s);
        Buffer.BlockCopy(b, 0, dst, offset, Math.Min(b.Length, max));
    }

    static byte[] LocalIP()
    {
        try
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                s.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)s.LocalEndPoint).Address.GetAddressBytes();
            }
        }
        catch { return new byte[] { 127, 0, 0, 1 }; }
    }
}
