using Qurre.API;
using System;
using static Qurre.API.Addons.BetterHints.Manager;
namespace BHA
{
    internal struct Action
    {
        internal readonly InjectAct<string, string, bool, Player> Act;
        internal readonly Guid Uid;
        internal Action(InjectAct<string, string, bool, Player> act)
        {
            Act = act;
            Uid = new();
        }
    }
}