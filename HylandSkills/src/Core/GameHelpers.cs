using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.PlayerScripts;

namespace HylandSkills
{
    public static class Money
    {
        public static float Cash
        {
            get
            {
                if (!NetworkSingleton<MoneyManager>.InstanceExists) return 0f;
                return NetworkSingleton<MoneyManager>.Instance.cashBalance;
            }
        }

        public static bool TrySpendCash(float amount)
        {
            if (!NetworkSingleton<MoneyManager>.InstanceExists) return false;
            var mm = NetworkSingleton<MoneyManager>.Instance;
            if (mm.cashBalance < amount) return false;
            mm.ChangeCashBalance(-amount, true, true);
            return true;
        }

        public static void AddCash(float amount)
        {
            if (!NetworkSingleton<MoneyManager>.InstanceExists) return;
            NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(amount, true, true);
        }

        public static float Online
        {
            get
            {
                if (!NetworkSingleton<MoneyManager>.InstanceExists) return 0f;
                return NetworkSingleton<MoneyManager>.Instance.onlineBalance;
            }
        }

        /// <summary>Pays from the bank account (shows up as a transaction in the game's online banking).</summary>
        public static bool TrySpendOnline(float amount, string name, string note)
        {
            if (!NetworkSingleton<MoneyManager>.InstanceExists || Online < amount) return false;
            NetworkSingleton<MoneyManager>.Instance.CreateOnlineTransaction(name, -amount, 1f, note);
            return true;
        }

        public static void AddOnline(float amount, string name, string note)
        {
            if (!NetworkSingleton<MoneyManager>.InstanceExists) return;
            NetworkSingleton<MoneyManager>.Instance.CreateOnlineTransaction(name, amount, 1f, note);
        }

        public static bool InPursuit
        {
            get
            {
                try
                {
                    var p = Player.Local;
                    return p != null && p.CrimeData != null && p.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None;
                }
                catch { return false; }
            }
        }
    }
}
