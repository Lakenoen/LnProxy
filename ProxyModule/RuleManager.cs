using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProxyModule;
public class RuleManager
{
    private List<RuleInfo> _allow = new List<RuleInfo>();
    private List<RuleInfo> _deny = new List<RuleInfo>();
    public RuleManager(string path)
    {
        Read(path);
    }
    private void Read(string path)
    {
        using FileStream stream = new FileStream(path, FileMode.Open);
        using StreamReader reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (line is null || line.StartsWith("#"))
                continue;
            RuleInfo rule = RuleInfo.Parse(line, out bool isAllow);
            if(isAllow)
                _allow.Add(rule);
            else
                _deny.Add(rule);
        }
    }

    public bool Check(RuleInfo info)
    {
        Task<int> denyIndexTask = Task.Run(() => _deny.FindIndex(rule =>
        {
            return rule.Equals(info);
        }));

        Task<int> allowIndexTask = Task.Run(() => _allow.FindIndex(rule =>
        {
            return rule.Equals(info);
        }));

        Task.WaitAll(denyIndexTask, allowIndexTask);

        if (denyIndexTask.Result < 0)
            return true;

        if(allowIndexTask.Result < 0)
            return false;

        return (denyIndexTask.Result <= allowIndexTask.Result) ? false : true;
    }
    public class RuleInfo : IEquatable<RuleInfo>
    {
        public string SourcePort { get; set; } = string.Empty;
        public string TargetPort { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Proto { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public static RuleInfo Parse(string line, out bool isAllow)
        {
            isAllow = false;

            var l = line.ToLower().Trim();
            RuleInfo res = new RuleInfo();
            string[] elems = l.Split(' ',StringSplitOptions.TrimEntries);

            if (elems.Length == 1)
                throw new ApplicationException("Rule parse error");

            int i = 0;
            if (l.StartsWith("allow"))
            {
                i++;
                isAllow = true;
            }
            if (l.StartsWith("deny"))
            {
                i++;
                isAllow = false;
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

        public bool Equals(RuleInfo? other)
        {
            if (other is null)
                return false;

            short trues = 0;
            trues += (new Regex(this.Source.Trim(), RegexOptions.IgnoreCase).Match(other.Source.Trim()).Success) ? (short)1 : (short)0;
            trues += (new Regex(this.SourcePort.Trim(), RegexOptions.IgnoreCase).Match(other.SourcePort.Trim()).Success) ? (short)1 : (short)0;
            trues += (new Regex(this.Target.Trim(), RegexOptions.IgnoreCase).Match(other.Target.Trim()).Success) ? (short)1 : (short)0;
            trues += (new Regex(this.TargetPort.Trim(), RegexOptions.IgnoreCase).Match(other.TargetPort.Trim()).Success) ? (short)1 : (short)0;
            trues += (new Regex(this.Proto.Trim(), RegexOptions.IgnoreCase).Match(other.Proto.Trim()).Success) ? (short)1 : (short)0;
            trues += (new Regex(this.Command.Trim(), RegexOptions.IgnoreCase).Match(other.Command.Trim()).Success) ? (short)1 : (short)0;
            if (this.Username == string.Empty)
                ++trues;
            else
                trues += (new Regex(this.Username.Trim(), RegexOptions.IgnoreCase).Match(other.Username.Trim()).Success) ? (short)1 : (short)0;

            return trues.Equals(7);
        }
    }

}
