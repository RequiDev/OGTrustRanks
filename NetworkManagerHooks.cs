using System;
using VRC;
using VRC.Core;

namespace OGTrustRanks
{
    internal static class NetworkManagerHooks
    {
        private static bool _isInitialized;
        private static bool _seenFire;
        private static bool _aFiredFirst;

        public static event Action<Player> OnJoin;
        public static event Action<Player> OnLeave;

        public static void EventHandlerA(Player player)
        {
            if (!_seenFire)
            {
                _aFiredFirst = true;
                _seenFire = true;
            }

            (_aFiredFirst ? OnJoin : OnLeave)?.Invoke(player);
        }

        public static void EventHandlerB(Player player)
        {
            if (!_seenFire)
            {
                _aFiredFirst = false;
                _seenFire = true;
            }

            (_aFiredFirst ? OnLeave : OnJoin)?.Invoke(player);
        }

        public static void Initialize()
        {
            if (_isInitialized) return;
            if (ReferenceEquals(NetworkManager.field_Internal_Static_NetworkManager_0, null)) return;

            var field0 = NetworkManager.field_Internal_Static_NetworkManager_0
                .field_Internal_VRCEventDelegate_1_Player_0;
            var field1 = NetworkManager.field_Internal_Static_NetworkManager_0
                .field_Internal_VRCEventDelegate_1_Player_1;

            AddDelegate(field0, EventHandlerA);
            AddDelegate(field1, EventHandlerB);

            _isInitialized = true;
        }

        private static void AddDelegate(VRCEventDelegate<Player> field, Action<Player> eventHandlerA)
        {
            field.field_Private_HashSet_1_UnityAction_1_T_0.Add(eventHandlerA);
        }
    }
}