using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProxyModule;
public class RuleManager
{
    private List<RuleInfo> _rules = new List<RuleInfo>();
    public RuleManager(string path)
    {
        _rules = Read(path);
    }
    private static List<RuleInfo> Read(string path)
    {
        List<RuleInfo> res =new List<RuleInfo>();
        using FileStream stream = new FileStream(path, FileMode.Open);
        using StreamReader reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (line is null || line.StartsWith("#"))
                continue;
            res.Add(RuleInfo.Parse(line));
        }

        res.Sort((rule1, rule2) =>
        {
            if (rule1.IsDeny) return -1;
            if (!rule1.IsDeny) return 1;
            return 0;
        });
        return res;
    }

    public bool Check(RuleInfo info)
    {
        foreach (RuleInfo rule in _rules)
        {
            short trues = 0;
            trues += (new Regex(rule.Source, RegexOptions.IgnoreCase).Match(info.Source).Success) ? (short)1 : (short)0;
            trues += (new Regex(rule.SourcePort, RegexOptions.IgnoreCase).Match(info.SourcePort).Success) ? (short)1 : (short)0;
            trues += (new Regex(rule.Target, RegexOptions.IgnoreCase).Match(info.Target).Success) ? (short)1 : (short)0;
            trues += (new Regex(rule.TargetPort, RegexOptions.IgnoreCase).Match(info.TargetPort).Success) ? (short)1 : (short)0;
            trues += (new Regex(rule.Proto, RegexOptions.IgnoreCase).Match(info.Proto).Success) ? (short)1 : (short)0;
            trues += (new Regex(rule.Command, RegexOptions.IgnoreCase).Match(info.Command).Success) ? (short)1 : (short)0;
            if (info.Username == string.Empty)
                ++trues;
            else
                trues += (new Regex(rule.Username, RegexOptions.IgnoreCase).Match(info.Username).Success) ? (short)1 : (short)0;

            if (trues.Equals(7))
            {
                return !rule.IsDeny;
            }
        }
        return false;
    }
    public class RuleInfo
    {
        public bool IsDeny { get; set; } = false;
        public string? SourcePort { get; set; }
        public string? TargetPort { get; set; }
        public string? Source { get; set; }
        public string? Target { get; set; }
        public string? Proto { get; set; }
        public string? Command { get; set; }
        public string? Username { get; set; }
        public static RuleInfo Parse(string line)
        {
            var l = line.ToLower().Trim();
            RuleInfo res = new RuleInfo();
            string[] elems = l.Split(' ');

            if (elems.Length == 1)
                throw new ApplicationException("Rule parse error");

            int i = 0;
            if (l.StartsWith("allow"))
            {
                i++;
                res.IsDeny = false;
            }
            if (l.StartsWith("deny"))
            {
                i++;
                res.IsDeny = true;
            }
            res.Source = elems[i++];
            res.SourcePort = elems[i++];
            res.Target = elems[i++];
            res.TargetPort = elems[i++];
            res.Proto = elems[i++];
            res.Command = elems[i++];
            res.Username = elems[i++];

            return res;
        }

    }

}
