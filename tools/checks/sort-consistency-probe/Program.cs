// RR-1 D-2 check (docs/gap-repair-rr1-spec-back.md): does .NET 8 List.Sort throw on the ST-1 comparator cycle?
// Run: dotnet run -c Release --project tools/checks/sort-consistency-probe/SortConsistencyProbe.csproj
using System; using System.Collections.Generic; using System.Linq;
record P(long Ts, long Seq);
class T {
  // The shipped RR-1 comparator (Core/TradeStoreWriter.vb:1041-1046), transcribed.
  static int Cmp(P a, P b) {
    if (a.Seq >= 0 && b.Seq >= 0) return a.Seq.CompareTo(b.Seq);
    int c = a.Ts.CompareTo(b.Ts); if (c != 0) return c;
    return a.Seq.CompareTo(b.Seq);
  }
  static void Run(string label, Func<Random, List<P>> make, int trials) {
    var rng = new Random(12345); var types = new Dictionary<string,int>(); int inconsistentOut = 0;
    for (int t = 0; t < trials; t++) {
      var l = make(rng).OrderBy(_ => rng.Next()).ToList();
      try { l.Sort(Cmp); bool ok = true; for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++) if (Cmp(l[i], l[j]) > 0) ok = false; if (!ok) inconsistentOut++; types["none"] = types.GetValueOrDefault("none") + 1; }
      catch (Exception e) { var k = e.GetType().Name; types[k] = types.GetValueOrDefault(k) + 1; }
    }
    Console.WriteLine($"{label}: trials={trials} outcomes=[{string.Join(", ", types.Select(kv => kv.Key + "=" + kv.Value))}] returnedOrderWithPairViolation={inconsistentOut}");
  }
  static void Main() {
    { var rng = new Random(7); int thr = 0; string kinds = ""; for (int t = 0; t < 300; t++) { var l = Enumerable.Range(0, 2000).ToList(); try { l.Sort((a, b) => a == b ? 0 : (rng.Next(2) == 0 ? -1 : 1)); } catch (Exception e) { thr++; kinds = e.GetType().Name; } } Console.WriteLine($"random comparator, 2000 ints: trials=300 threw={thr} lastType={kinds}"); }
    Console.WriteLine(".NET " + Environment.Version);
    // A91c as shipped: legacy@50000, N+1@100001, N+3@100000 (invariant-respecting, no cycle possible).
    Run("A91c shipped rows (3)", r => new() { new(50000,-1), new(100001,9001), new(100000,9003) }, 2000);
    // ST-1 cycle (spec section 4.2): A(seq100,t500), B legacy t200, C(seq200,t100).
    Run("ST-1 cycle, 3 rows", r => new() { new(500,100), new(200,-1), new(100,200) }, 2000);
    // Same cycle padded to 17 and 200 rows (introsort proper above 16).
    foreach (int n in new[] { 17, 40, 200, 2000 })
      Run($"ST-1 cycles repeated, {n} rows", r => { var l = new List<P>(); int k = 0; while (l.Count < n) { long b = 1000L * k++; l.Add(new(b+500, 100+10*k)); if (l.Count < n) l.Add(new(b+200, -1)); if (l.Count < n) l.Add(new(b+100, 105+10*k)); } return l; }, 500);
  }
}
