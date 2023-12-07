using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Toolbox
{
    public static string GetBytesReadable(long i)
    {
        // Get absolute value
        long absolute_i = (i < 0 ? -i : i);
        // Determine the suffix and readable value
        string suffix;
        double readable;
        if (absolute_i >= 0x1000000000000000) // Exabyte
        {
            suffix = "EB";
            readable = (i >> 50);
        }
        else if (absolute_i >= 0x4000000000000) // Petabyte
        {
            suffix = "PB";
            readable = (i >> 40);
        }
        else if (absolute_i >= 0x10000000000) // Terabyte
        {
            suffix = "TB";
            readable = (i >> 30);
        }
        else if (absolute_i >= 0x40000000) // Gigabyte
        {
            suffix = "GB";
            readable = (i >> 20);
        }
        else if (absolute_i >= 0x100000) // Megabyte
        {
            suffix = "MB";
            readable = (i >> 10);
        }
        else if (absolute_i >= 0x400) // Kilobyte
        {
            suffix = "KB";
            readable = i;
        }
        else
        {
            return i.ToString("0 B"); // Byte
        }
        // Divide by 1024 to get fractional value
        readable = (readable / 1024);
        // Return formatted number with suffix
        return readable.ToString("0.### ") + suffix;
    }

    public static string FormatLargeNumber(long number)
    {
        if (number < 10000)
        {
            return number.ToString();
        }
        if (number < 1000000)
        {
            return string.Format("{0:n2} {1}", number / 1000.0, "k");
        }
        if (number < 1000000000)
        {
            return string.Format("{0:n2} {1}", number / 1000000.0, "M");
        }
        return string.Format("{0:n2} {1}", number / 1000000000.0, "G");
    }

    public static string FormatLargeNumber(this BigInteger value, int decimalPlaces = 2)
    {
        string[] units = { "", "K", "M", "G", "T", "P", "E", "Z", "Y" };

        int unitIndex = 0;
        BigInteger remainingValue = value;
        BigInteger divisor = new BigInteger(1000);
        while (remainingValue >= divisor)
        {
            remainingValue /= divisor;
            unitIndex++;
        }

        BigInteger scaleFactor = BigInteger.Pow(10, decimalPlaces);
        BigInteger scaledValue = (value * scaleFactor) / BigInteger.Pow(divisor, unitIndex);
        double doubleScaledValue = (double) scaledValue / Math.Pow(10, decimalPlaces);

        string formatString = "{0:F" + decimalPlaces + "} {1}";
        return string.Format(formatString, doubleScaledValue, units[unitIndex]);
    }

    public static void DestroyChildren(this Transform transform)
    {
        //Add children to list before destroying
        //otherwise GetChild(i) may bomb out
        var children = new List<Transform>();

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            children.Add(child);
        }

        foreach (var child in children)
        {
            Object.Destroy(child.gameObject);
        }
    }

    public static bool SmartActive(this Component c, bool active)
    {
        SmartActive(c.gameObject, active);
        return active;
    }

    public static void SmartActive(this GameObject go, bool active)
    {
        if (go.activeSelf != active) go.SetActive(active);
    }

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        while (!condition() && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
            if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                throw new TimeoutException("WaitUntilAsync timed out.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int Min(Vector2Int a, Vector2Int b, Vector2Int c)
    {
        return new Vector2Int(Mathf.Min(a.x, b.x, c.x), Mathf.Min(a.y, b.y, c.y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int Max(Vector2Int a, Vector2Int b, Vector2Int c)
    {
        return new Vector2Int(Mathf.Max(a.x, b.x, c.x), Mathf.Max(a.y, b.y, c.y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int Min(Vector2Int a, Vector2Int b)
    {
        return new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int Max(Vector2Int a, Vector2Int b)
    {
        return new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int Clamp(Vector2Int x, Vector2Int a, Vector2Int b)
    {
        return Max(a, Min(b, x));
    }

    /// <summary>
    /// Supports both , and .
    /// </summary>
    public static float ParseFloat(string s)
    {
        if (s.Contains(","))
            return float.Parse(s.Replace(",", "."), CultureInfo.InvariantCulture);
        return float.Parse(s, CultureInfo.InvariantCulture);
    }
}