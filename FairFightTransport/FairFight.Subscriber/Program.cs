using System.Net;
using System.Net.Sockets;
using FairFight.Common;

int port = args.Length > 0 ? int.Parse(args[0]) : 9050;

using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
udp.Client.ReceiveBufferSize = 4 * 1024 * 1024; // avoid OS-level drops at high rates

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"Listening on UDP port {port}. Ctrl+C to stop.");

var lastSeq = new Dictionary<int, uint>();
var window = new List<double>();   // latencies (ms) in the current 1 s window
var all = new List<double>();      // latencies for the whole run
long received = 0, lost = 0, outOfOrder = 0, wrongData = 0, badSize = 0;
long windowStart = Environment.TickCount64;
bool verbose = true;               // print individual messages until traffic gets heavy

try
{
    while (!cts.IsCancellationRequested)
    {
        var result = await udp.ReceiveAsync(cts.Token);
        long nowTicks = DateTime.UtcNow.Ticks;

        if (result.Buffer.Length != EntityUpdate.Size) { badSize++; continue; }

        var m = EntityUpdate.ReadFrom(result.Buffer);
        received++;

        // 1. Correctness: compare against the deterministic expected value.
        var (ex, ey, ez) = EntityUpdate.ExpectedPosition(m.EntityId, m.Sequence);
        bool ok = m.X == ex && m.Y == ey && m.Z == ez;
        if (!ok) wrongData++;

        // 2. Loss / reordering, tracked per entity.
        if (lastSeq.TryGetValue(m.EntityId, out var last))
        {
            if (m.Sequence > last + 1) lost += m.Sequence - last - 1;
            if (m.Sequence <= last) outOfOrder++;
            else lastSeq[m.EntityId] = m.Sequence;
        }
        else lastSeq[m.EntityId] = m.Sequence;

        // 3. One-way latency (valid only if sender and receiver share a clock).
        double latMs = (nowTicks - m.SentUtcTicks) / (double)TimeSpan.TicksPerMillisecond;
        window.Add(latMs);
        all.Add(latMs);

        if (verbose && received <= 20)
            Console.WriteLine($"RECV seq={m.Sequence} id={m.EntityId} " +
                              $"pos=({m.X}, {m.Y}, {m.Z}) {(ok ? "OK" : "MISMATCH")} lat={latMs:F3} ms");

        if (Environment.TickCount64 - windowStart >= 1000)
        {
            Console.WriteLine($"[1s] {window.Count} msg | {Stats(window)} | total={received} lost={lost} ooo={outOfOrder} wrong={wrongData}");
            window.Clear();
            windowStart = Environment.TickCount64;
        }
    }
}
catch (OperationCanceledException) { }

Console.WriteLine();
Console.WriteLine("=== SUMMARY ===");
Console.WriteLine($"Received: {received}  Lost: {lost}  Out-of-order: {outOfOrder}  Wrong data: {wrongData}  Bad size: {badSize}");
if (all.Count > 0) Console.WriteLine($"Latency (ms): {Stats(all)}");

static string Stats(List<double> v)
{
    if (v.Count == 0) return "n/a";
    var s = v.OrderBy(x => x).ToList();
    double p95 = s[(int)Math.Min(s.Count - 1, s.Count * 0.95)];
    return $"lat ms min={s[0]:F3} avg={s.Average():F3} p95={p95:F3} max={s[^1]:F3}";
}