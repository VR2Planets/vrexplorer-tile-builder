using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class TimeMeasure
{
    private Dictionary<string, Stopwatch> watches = new();
    private Dictionary<string, int> counts = new();

    public void StartMeasure(string name)
    {
        if (!watches.ContainsKey(name))
        {
            watches[name] = new Stopwatch();
            counts[name] = 0;
        }

        watches[name].Start();
        counts[name]++;
    }

    public long StopMeasure(string name)
    {
        if (watches.ContainsKey(name))
        {
            watches[name].Stop();
            return watches[name].ElapsedMilliseconds;
        }
        return -1;
    }

    public void ResetMeasure(string name)
    {
        if (watches.ContainsKey(name))
        {
            watches[name].Reset();
            counts[name] = 0;
        }
    }

    public void ResetAllMeasures()
    {
        foreach (var measure in watches.Values)
        {
            measure.Reset();
        }
    }

    public string GetReport(bool reset = false)
    {
        StringBuilder strB = new StringBuilder();
        strB.AppendLine("Time Measures Report:");
        foreach (var (key, measure) in watches)
        {
            strB.AppendLine($"- {key}: {measure.ElapsedMilliseconds} ms (×{counts[key]}).");
        }
        if (reset) ResetAllMeasures();
        return strB.ToString();
    }
}