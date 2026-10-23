using System.Diagnostics;
using System.Net.Sockets;
using FairFight.Common;

double rateHz = args.Length > 0 ? double.Parse(args[0]) : 10;
int    ticks  = args.Length > 1 ? int.Parse(args[1])    : 50;
int    count  = args.Length > 2 ? int.Parse(args[2])    : 1;
string host   = args.Length > 3 ? args[3]               : "127.0.0.1";
int    port   = args.Length > 4 ? int.Parse(args[4])    : 9050;

using var udp = new UdpClient();
udp.Connect(host, port);

Console.WriteLine($"Publishing {count} entities x {ticks} ticks at {rateHz} Hz to {host}:{port}");

var buffer = new byte[EntityUpdate.Size];
long intervalTicks = (long)(Stopwatch.Frequency / rateHz);
long start = Stopwatch.GetTimestamp();
long sent = 0;

for (uint seq = 0; seq < ticks; seq++)
{
    // Pace against absolute deadlines so timing error doesn't accumulate.
    long deadline = start + seq * intervalTicks;
    while (true)
    {
        long remaining = deadline - Stopwatch.GetTimestamp();
        if (remaining <= 0) break;
        if (remaining > Stopwatch.Frequency / 500) Thread.Sleep(1); // >2 ms left
        else Thread.SpinWait(50);
    }

    for (int id = 1; id <= count; id++)
    {
        var (x, y, z) = EntityUpdate.ExpectedPosition(id, seq);
        var msg = new EntityUpdate(seq, id, x, y, z, DateTime.UtcNow.Ticks);
        msg.WriteTo(buffer);
        udp.Send(buffer, buffer.Length);
        sent++;

        if (count == 1 && ticks <= 100)
            Console.WriteLine($"SENT seq={seq} id={id} pos=({x}, {y}, {z})");
    }
}

double secs = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
Console.WriteLine($"Done. Sent {sent} messages in {secs:F2}s ({sent / secs:F0} msg/s actual).");