using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    public enum EnvLineKind { Assignment, Comment, Blank, PlainText }

    public sealed class EnvLine
    {
        public string      Raw    { get; set; }
        public EnvLineKind Kind   { get; set; }
        public string      Key    { get; set; }
        public string      Value  { get; set; }
        public string      Prefix { get; set; }

        public string DisplayText(bool reveal)
        {
            if (Kind != EnvLineKind.Assignment) return Raw;
            return Prefix + Key + "=" + (reveal ? Value : Mask(Value));
        }

        private static string Mask(string v)
        {
            if (string.IsNullOrEmpty(v)) return string.Empty;
            if (v.Length >= 2)
            {
                char f = v[0], l = v[v.Length - 1];
                if ((f == '"' && l == '"') || (f == '\'' && l == '\'') || (f == '`' && l == '`'))
                    return f + new string('*', Math.Max(8, v.Length - 2)) + l;
            }
            return new string('*', Math.Max(8, v.Length));
        }
    }

    public static class EnvMaskingService
    {
        private static readonly Regex RxAssign = new Regex(
            @"^(\s*(?:export\s+)?)([A-Za-z_][A-Za-z0-9_]*)=(.*)$",
            RegexOptions.Compiled);

        private static readonly Regex RxComment = new Regex(
            @"^\s*#", RegexOptions.Compiled);

        public static List<EnvLine> Parse(string raw)
        {
            if (raw == null) throw new ArgumentNullException("raw");

            var lines  = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<EnvLine>(lines.Length);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.Add(new EnvLine { Raw = line, Kind = EnvLineKind.Blank });
                    continue;
                }
                if (RxComment.IsMatch(line))
                {
                    result.Add(new EnvLine { Raw = line, Kind = EnvLineKind.Comment });
                    continue;
                }
                var m = RxAssign.Match(line);
                if (m.Success)
                {
                    result.Add(new EnvLine
                    {
                        Raw    = line,
                        Kind   = EnvLineKind.Assignment,
                        Prefix = m.Groups[1].Value,
                        Key    = m.Groups[2].Value,
                        Value  = m.Groups[3].Value
                    });
                    continue;
                }
                result.Add(new EnvLine { Raw = line, Kind = EnvLineKind.PlainText });
            }

            return result;
        }
    }
}