using MEC;
using Qurre.API;
using Qurre.API.Addons.BetterHints;
using System;
using static Qurre.API.Addons.BetterHints.Manager;
namespace Qurre
{
    using BHA;
    public class HintSender : ISender
    {
        public void Hint(Player pl, HintStruct hs)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            if (!Manager.Hints.ContainsKey(pl)) Manager.Hints.Add(pl, new());
            var list = Manager.Hints[pl];
            list.Add(hs);
            Timing.CallDelayed(hs.Duration, () => list.Remove(hs));
        }

        public Guid InjectAction(Player pl, InjectAct<string, string, bool, Player> act)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            Action action = new(act);
            if (Manager.Actions.TryGetValue(pl, out var list)) list.Add(action);
            else Manager.Actions.Add(pl, new() { action });
            return action.Uid;
        }
        public bool UnjectAction(Player pl, Guid uid)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            if (Manager.Actions.TryGetValue(pl, out var list)) return false;
            if (!list.TryFind(out var act, x => x.Uid == uid)) return false;
            list.Remove(act);
            return true;
        }
        public bool ContainsAction(Player pl, Guid uid)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            if (Manager.Actions.TryGetValue(pl, out var list)) return false;
            if (!list.TryFind(out var act, x => x.Uid == uid)) return false;
            return true;
        }

        public Guid InjectGlobalAction(InjectAct<string, string, bool, Player> act)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            Action action = new(act);
            Manager.GlobalActions.Add(action);
            return action.Uid;
        }
        public bool UnjectGlobalAction(Guid uid)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            if (!Manager.GlobalActions.TryFind(out var act, x => x.Uid == uid)) return false;
            Manager.GlobalActions.Remove(act);
            return true;
        }
        public bool ContainsGlobalAction(Guid uid)
        {
            if (!BHA.Loader.BetterHints) throw new Exception("Better Hints disabled in config");
            if (!Manager.GlobalActions.TryFind(out var act, x => x.Uid == uid)) return false;
            return true;
        }
    }
}