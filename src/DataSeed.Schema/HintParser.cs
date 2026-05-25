using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DataSeed.Schema.Models;

namespace DataSeed.Schema;

public static class HintParser
{
    public static HintSet Parse(IEnumerable<string> hints)
    {
        var result = new HintSet();

        foreach (var raw in hints)
        {
            var hint = raw.Trim();

            if (hint == "unique")
            {
                result.Unique = true;
                continue;
            }

            var m = Regex.Match(hint, @"^nullable:\s*(\d+)%$");
            if (m.Success) { result.NullablePercent = int.Parse(m.Groups[1].Value); continue; }

            m = Regex.Match(hint, @"^degradable:\s*(\d+)%$");
            if (m.Success) { result.DegradablePercent = int.Parse(m.Groups[1].Value); continue; }

            m = Regex.Match(hint, @"^derived:\s*""?(.+?)""?$");
            if (m.Success) { result.DerivedTemplate = m.Groups[1].Value.Trim('"'); continue; }

            m = Regex.Match(hint, @"^values:\s*\[(.+?)\]$");
            if (m.Success)
            {
                foreach (var v in m.Groups[1].Value.Split(','))
                    result.Values.Add(v.Trim());
                continue;
            }

            m = Regex.Match(hint, @"^range:\s*(\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)$");
            if (m.Success)
            {
                result.Range = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                continue;
            }

            m = Regex.Match(hint, @"^distribution:\s*(.+)$");
            if (m.Success) { result.Distribution = m.Groups[1].Value.Trim(); continue; }

            m = Regex.Match(hint, @"^skew:\s*([\d.]+)$");
            if (m.Success) { result.Skew = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); continue; }

            if (hint == "depth: leaf") { result.DepthLeaf = true; continue; }

            m = Regex.Match(hint, @"^depth:\s*(\d+)$");
            if (m.Success) { result.DepthLevel = int.Parse(m.Groups[1].Value); continue; }

            m = Regex.Match(hint, @"^linesPerParent:\s*(\d+)-(\d+)$");
            if (m.Success)
            {
                result.LinesPerParent = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                continue;
            }

            m = Regex.Match(hint, @"^dateRange:\s*""?(\d{4}-\d{2}-\d{2})/(\d{4}-\d{2}-\d{2})""?$");
            if (m.Success)
            {
                result.DateRange = (
                    DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTime.ParseExact(m.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                );
                continue;
            }
        }

        return result;
    }
}
