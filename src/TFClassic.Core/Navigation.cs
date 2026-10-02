using System.Numerics;

namespace TFClassic.Core;

public sealed class NavNode
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public Vector3 Position { get; init; }
    public List<int> Links { get; } = new();
}

/// <summary>Hand-authored waypoint graph that bots path over.</summary>
public sealed class NavGraph
{
    public List<NavNode> Nodes { get; } = new();
    readonly Dictionary<string, int> byName = new();

    public int Add(string name, Vector3 pos)
    {
        var node = new NavNode { Id = Nodes.Count, Name = name, Position = pos };
        Nodes.Add(node);
        byName[name] = node.Id;
        return node.Id;
    }

    public int Find(string name) => byName[name];

    public void Link(string a, string b)
    {
        int ia = byName[a], ib = byName[b];
        Nodes[ia].Links.Add(ib);
        Nodes[ib].Links.Add(ia);
    }

    public int Nearest(Vector3 pos, World? world = null)
    {
        int best = -1, bestAny = 0;
        float bestD = float.MaxValue, bestAnyD = float.MaxValue;
        foreach (var n in Nodes)
        {
            float d = Vector3.DistanceSquared(n.Position, pos);
            if (d < bestAnyD) { bestAnyD = d; bestAny = n.Id; }
            if (d < bestD && (world == null ||
                world.LineOfSight(pos + new Vector3(0, 36, 0), n.Position + new Vector3(0, 36, 0))))
            {
                bestD = d;
                best = n.Id;
            }
        }
        return best >= 0 ? best : bestAny;
    }

    /// <summary>A* over the graph. noise (0..1) randomly inflates edge costs so bots vary their routes.</summary>
    public List<int> FindPath(int start, int goal, Random? rng = null, float noise = 0f)
    {
        var path = new List<int>();
        if (start == goal) { path.Add(start); return path; }

        var open = new PriorityQueue<int, float>();
        var cost = new Dictionary<int, float> { [start] = 0 };
        var from = new Dictionary<int, int>();
        var edgeNoise = new Dictionary<(int, int), float>();
        open.Enqueue(start, 0);

        while (open.TryDequeue(out int cur, out _))
        {
            if (cur == goal) break;
            foreach (int next in Nodes[cur].Links)
            {
                float step = Vector3.Distance(Nodes[cur].Position, Nodes[next].Position);
                if (noise > 0f && rng != null)
                {
                    var key = (Math.Min(cur, next), Math.Max(cur, next));
                    if (!edgeNoise.TryGetValue(key, out float k))
                        edgeNoise[key] = k = 1f + (float)rng.NextDouble() * noise;
                    step *= k;
                }
                float nc = cost[cur] + step;
                if (!cost.TryGetValue(next, out float old) || nc < old)
                {
                    cost[next] = nc;
                    from[next] = cur;
                    open.Enqueue(next, nc + Vector3.Distance(Nodes[next].Position, Nodes[goal].Position));
                }
            }
        }

        if (!from.ContainsKey(goal)) return path;
        for (int n = goal; n != start; n = from[n]) path.Add(n);
        path.Add(start);
        path.Reverse();
        return path;
    }
}
