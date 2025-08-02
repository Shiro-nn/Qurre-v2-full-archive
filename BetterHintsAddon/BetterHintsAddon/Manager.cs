using Hints;
using MEC;
using Qurre;
using Qurre.API;
using Qurre.API.Addons.BetterHints;
using System;
using System.Collections.Generic;
namespace BHA
{
    internal static class Manager
    {
        internal static readonly Dictionary<Player, List<HintStruct>> Hints = new();
        internal static readonly Dictionary<Player, List<string>> FixHints = new();
        internal static readonly Dictionary<Player, List<Action>> Actions = new();
        internal static readonly List<Action> GlobalActions = new();
        internal static void ClearFields()
        {
            Hints.Clear();
            FixHints.Clear();
            Actions.Clear();
            GlobalActions.Clear();
        }

        internal static void ShowHint(Player pl, string text, float dur)
        {
            if (!FixHints.ContainsKey(pl)) FixHints.Add(pl, new List<string>());
            var list = text.Trim().Split('\n');
            foreach (var str in list)
            {
                string _ = str.Replace("\n", "").Trim();
                if (_ == "") continue;
                if (!FixHints.TryGetValue(pl, out var _data)) continue;
                _data.Add(_);
                Timing.CallDelayed(dur, () => { if (_data.Contains(_)) _data.Remove(_); });
            }
        }

        internal static IEnumerator<float> Сycle()
        {
            for (; ; )
            {
                try { CycleVoid(); } catch { }
                yield return Timing.WaitForSeconds(1f);
            }
        }
        private static void CycleVoid()
        {
            if (!Loader.BetterHints) return;
            foreach (Player pl in Player.List)
            {
                try
                {
                    int _count = 7;
                    string FormateArr(string str)
                    {
                        string ret = "";
                        var sta = str.Split('\n');
                        foreach (string st in sta)
                        {
                            if (!string.IsNullOrEmpty(st)) ret += $"<voffset={_count + GetVOffset(st)}em>{st}</voffset>";
                            _count -= 1;
                            if (_count < -20) _count = 7;
                        }
                        return ret + "\n";
                    }
                    float GetVOffset(string text)
                    {
                        if (text.Contains("<voffset="))
                        {
                            foreach (string spl in text.Split('>'))
                            {
                                if (spl.Contains("<voffset="))
                                {
                                    return float.Parse(spl.Substring(spl.IndexOf("<voffset=") + 9, spl.Contains("em") ? spl.Length - 11 : spl.Length - 9), System.Globalization.CultureInfo.InvariantCulture);
                                }
                            }
                        }
                        return 0;
                    }
                    string str = "<line-height=0%>";
                    List<string> fix = new();
                    try { fix.AddRange(FixHints[pl]); } catch { }
                    if (fix.Count > 0)
                    {
                        _count = 7;
                        for (int i = 0; fix.Count > i; i++)
                            if (fix[i] != "")
                                str += FormateArr(fix[i]);
                    }
                    List<HintStruct> _hs = new();
                    try { _hs.AddRange(Hints[pl]); } catch { }
                    if (_hs.Count > 0)
                    {
                        _count = 0;
                        foreach (var _h in _hs)
                        {
                            int _hc = 0;
                            var msgs = _h.Message.Split('\n');
                            foreach (string msg in msgs)
                            {
                                str += $"<voffset={(_h.Static ? Math.Max(_h.Voffset + _hc, -22) : Math.Max(_count + _h.Voffset + _hc, -22))}em><pos={_h.Position}%>{msg}</pos></voffset>";
                                _hc -= 1;
                                if (_hc < -20) _hc = 0;
                                _count -= 1;
                                if (_count < -20) _count = 0;
                            }
                        }
                    }
                    _count = 7;
                    if (Actions.TryGetValue(pl, out var acls))
                    {
                        for (int i = 0; i < acls.Count; i++)
                        {
                            try
                            {
                                acls[i].Act(str, out string add, out bool af, pl);
                                if (af) str += FormateArr(add);
                                else str += add;
                            }
                            catch (Exception e)
                            {
                                ServerConsole.AddLog($"[ERROR] Addons > BetterHints [Update]:\n{e}\n{e.StackTrace}", ConsoleColor.Red);
                            }
                        }
                    }
                    {
                        for (int i = 0; i < GlobalActions.Count; i++)
                        {
                            try
                            {
                                GlobalActions[i].Act(str, out string add, out bool af, pl);
                                if (af) str += FormateArr(add);
                                else str += add;
                            }
                            catch (Exception e)
                            {
                                ServerConsole.AddLog($"[ERROR] Addons > BetterHints [Global Update]:\n{e}\n{e.StackTrace}", ConsoleColor.Red);
                            }
                        }
                    }
                    pl.Client.HintDisplay.Show(new TextHint(str, new HintParameter[] { new StringHintParameter(Loader.HintToken) }, null, 1.1f));
                }
                catch (Exception e) { Log.Error(e); }
            }
        }
    }
}