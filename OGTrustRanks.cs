using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using VRC;
using VRC.Core;
using MelonLoader;
using HarmonyLib;
using System.Linq;
using System;
using UnhollowerRuntimeLib.XrefScans;
using Random = System.Random;

namespace OGTrustRanks
{
    public static class BuildInfo
    {
        public const string Name = "OGTrustRanks";
        public const string Author = "Herp Derpinstine, Emilia, dave-kun, and Requi";
        public const string Company = "Lava Gang";
        public const string Version = "1.1.6";
        public const string DownloadLink = "https://github.com/RequiDev/OGTrustRanks";
    }

    public class OGTrustRanks : MelonMod
    {
        private static Color _veteranUserColor;
        private static Color _legendaryUserColor;
        private static Color _knownUserColor;
        private static Color _trustedUserColor;
        private static MelonPreferences_Entry<bool> _enabledPref;
        private static MelonPreferences_Entry<int> _veteranColorRPref;
        private static MelonPreferences_Entry<int> _veteranColorGPref;
        private static MelonPreferences_Entry<int> _veteranColorBPref;
        private static MelonPreferences_Entry<int> _legendaryColorRPref;
        private static MelonPreferences_Entry<int> _legendaryColorGPref;
        private static MelonPreferences_Entry<int> _legendaryColorBPref;
        private static MelonPreferences_Entry<int> _trustedColorRPref;
        private static MelonPreferences_Entry<int> _trustedColorGPref;
        private static MelonPreferences_Entry<int> _trustedColorBPref;
        private static MelonPreferences_Entry<int> _knownColorRPref;
        private static MelonPreferences_Entry<int> _knownColorGPref;
        private static MelonPreferences_Entry<int> _knownColorBPref;
        private static MelonPreferences_Entry<bool> _reloadAvatar;
        private static readonly List<APIUser> CachedApiUsers = new List<APIUser>();
        private static readonly Queue<string> UsersToFetch = new Queue<string>();
        private static readonly Random Random = new Random();

        private static MethodBase _showSocialRankMethod;
        private static MethodInfo _reloadAvatarMethod;

        public override void OnApplicationStart()
        {
            var cat = MelonPreferences.CreateCategory("ogtrustranks", "OGTrustRanks");
            _enabledPref = cat.CreateEntry("enabled", true, "Enabled");

            _knownColorRPref = cat.CreateEntry("KnownColorR", 255, "Red component of the Known color");
            _knownColorGPref = cat.CreateEntry("KnownColorG", 122, "Green component of the Known color");
            _knownColorBPref = cat.CreateEntry("KnownColorB", 66, "Blue component of the Known color");

            _trustedColorRPref = cat.CreateEntry("TrustedColorR", 130, "Red component of the Trusted color");
            _trustedColorGPref = cat.CreateEntry("TrustedColorG", 66, "Green component of the Trusted color");
            _trustedColorBPref = cat.CreateEntry("TrustedColorB", 230, "Blue component of the Trusted color");
            
            _veteranColorRPref = cat.CreateEntry("VeteranColorR", 171, "Red component of the Veteran color");
            _veteranColorGPref = cat.CreateEntry("VeteranColorG", 205, "Green component of the Veteran color");
            _veteranColorBPref = cat.CreateEntry("VeteranColorB", 239, "Blue component of the Veteran color");

            _legendaryColorRPref = cat.CreateEntry("LegendaryColorR", 255, "Red component of the Legendary color");
            _legendaryColorGPref = cat.CreateEntry("LegendaryColorG", 105, "Green component of the Legendary color");
            _legendaryColorBPref = cat.CreateEntry("LegendaryColorB", 180, "Blue component of the Legendary color");

            _reloadAvatar = cat.CreateEntry("ReloadAvatar", false,
                "Reload avatars when fetched rank to update colors for BTKANameplateMod");

            UpdateColors();

            var harmony = new HarmonyLib.Harmony("OGTrustRanks");
            foreach (var method in typeof(VRCPlayer).GetMethods())
            {
                if (!method.Name.StartsWith("Method_Public_Static_String_APIUser_") || method.Name.Length != 37)
                    continue;

                if (_showSocialRankMethod == null)
                {
                    var xrefInstance = XrefScanner.XrefScan(method).FirstOrDefault(x =>
                    {
                        if (x.Type != XrefType.Method)
                            return false;

                        var m = x.TryResolve();
                        if (m == null || !m.IsStatic || m.DeclaringType != typeof(VRCPlayer))
                            return false;

                        var asInfo = m as MethodInfo;
                        if (asInfo == null || asInfo.ReturnType != typeof(bool))
                            return false;

                        if (m.GetParameters().Length != 1 && m.GetParameters()[0].ParameterType != typeof(APIUser))
                            return false;

                        return XrefScanner.XrefScan(m).Count() > 1;
                    });
                    if (xrefInstance.Type == XrefType.Method)
                    {
                        _showSocialRankMethod = xrefInstance.TryResolve();
                    }
                }

                harmony.Patch(method, new HarmonyMethod(typeof(OGTrustRanks).GetMethod(nameof(GetFriendlyDetailedNameForSocialRank), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            var colorForRankTargetMethods = typeof(VRCPlayer).GetMethods().Where(it => it.ReturnType.ToString().Equals("UnityEngine.Color") && it.GetParameters().Length == 1 && it.GetParameters()[0].ParameterType.ToString().Equals("VRC.Core.APIUser")).ToList();
            colorForRankTargetMethods.ForEach(it =>
                harmony.Patch(it, new HarmonyMethod(typeof(OGTrustRanks).GetMethod(nameof(GetColorForSocialRank), BindingFlags.NonPublic | BindingFlags.Static)))
            );

            // Thanks loukylor
            _reloadAvatarMethod = typeof(VRCPlayer).GetMethods().First(mi =>
                mi.Name.StartsWith("Method_Private_Void_Boolean_") && mi.Name.Length < 31 &&
                mi.GetParameters().Any(pi => pi.IsOptional));


            MelonCoroutines.Start(InitializeNetworkHooks());
            MelonCoroutines.Start(FetchAPIUsers());
        }

        private IEnumerator InitializeNetworkHooks()
        {
            while (ReferenceEquals(NetworkManager.field_Internal_Static_NetworkManager_0, null)) yield return null;
            while (ReferenceEquals(VRCAudioManager.field_Private_Static_VRCAudioManager_0, null)) yield return null;
            while (ReferenceEquals(VRCUiManager.prop_VRCUiManager_0, null)) yield return null;

            NetworkManagerHooks.Initialize();
            NetworkManagerHooks.OnJoin += OnPlayerJoin;
        }

        public void OnPlayerJoin(Player player)
        {
            if (player == null)
                return;
            var apiUser = player.prop_APIUser_0;
            if (apiUser == null)
                return;

            if (!apiUser.tags.Contains("system_trust_trusted"))
                return;

            if (CachedApiUsers.Exists(x => x.id == apiUser.id))
                return;

            if (UsersToFetch.Contains(apiUser.id))
                return;
            
            UsersToFetch.Enqueue(apiUser.id);
        }

        private static IEnumerator FetchAPIUsers()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                while (UsersToFetch.Count > 0)
                {
                    var id = UsersToFetch.Dequeue();
                    APIUser.FetchUser(id, new Action<APIUser>(user =>
                    {
                        CachedApiUsers.Add(user);
                        if (_reloadAvatar.Value)
                        {
                            var player = GetPlayerByUserId(id);
                            _reloadAvatarMethod.Invoke(player._vrcplayer, new object[] {true});
                        }
                    }), new Action<string>(error =>
                    {
                        MelonLogger.Error($"Could not fetch APIUser object of {id}");
                    }));
                    yield return new WaitForSeconds(Random.Next(2, 5));
                }
            }
        }

        public override void OnPreferencesSaved() => Refresh();
        public override void OnPreferencesLoaded() => Refresh();
        public override void OnSceneWasInitialized(int buildindex, string name) => Refresh();

        private static void Refresh()
        {
            UpdateColors();
        }

        private static void UpdateColors()
        {
            _knownUserColor = new Color(_knownColorRPref.Value / 255.0f, _knownColorGPref.Value / 255.0f, _knownColorBPref.Value / 255.0f);
            _trustedUserColor = new Color(_trustedColorRPref.Value / 255.0f, _trustedColorGPref.Value / 255.0f, _trustedColorBPref.Value / 255.0f);
            _veteranUserColor = new Color(_veteranColorRPref.Value / 255.0f, _veteranColorGPref.Value / 255.0f, _veteranColorBPref.Value / 255.0f);
            _legendaryUserColor = new Color(_legendaryColorRPref.Value / 255.0f, _legendaryColorGPref.Value / 255.0f, _legendaryColorBPref.Value / 255.0f);
        }

        private static bool GetFriendlyDetailedNameForSocialRank(APIUser __0, ref string __result)
        {
            if (__0 == null || !_enabledPref.Value) return true;

            if (GetPlayerByUserId(__0.id) != null)
            {
                var showSocialRank = (bool)_showSocialRankMethod.Invoke(null, new object[] { __0 });
                if (!showSocialRank)
                {
                    return true;
                }
            }
            
            var apiUser = CachedApiUsers.Find(x => x.id == __0.id) ?? __0;
            var rank = GetTrustRankEnum(apiUser);

            if (rank == TrustRanks.Ignore) return true;

            __result = $"{rank} User";
            return false;
        }

        private static bool GetColorForSocialRank(APIUser __0, ref Color __result)
        {
            if (__0 == null || !_enabledPref.Value || APIUser.IsFriendsWith(__0.id)) return true;

            if (GetPlayerByUserId(__0.id) != null)
            {
                var showSocialRank = (bool)_showSocialRankMethod.Invoke(null, new object[] { __0 });
                if (!showSocialRank)
                {
                    return true;
                }
            }
            
            var apiUser = CachedApiUsers.Find(x => x.id == __0.id) ?? __0;
            var rank = GetTrustRankEnum(apiUser);
            switch (rank)
            {
                case TrustRanks.Known:
                    __result = _knownUserColor;
                    return false;
                case TrustRanks.Trusted:
                    __result = _trustedUserColor;
                    return false;
                case TrustRanks.Veteran:
                    __result = _veteranUserColor;
                    return false;
                case TrustRanks.Legendary:
                    __result = _legendaryUserColor;
                    return false;
                case TrustRanks.Ignore:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return true;
        }

        private static TrustRanks GetTrustRankEnum(APIUser user)
        {
            if (user?.tags == null || user.tags.Count <= 0)
                return TrustRanks.Ignore;

            if (user.tags.Contains("system_legend") && user.tags.Contains("system_trust_legend") && user.tags.Contains("system_trust_trusted"))
                return TrustRanks.Legendary;
            if (user.tags.Contains("system_trust_legend") && user.tags.Contains("system_trust_trusted"))
                return TrustRanks.Veteran;
            if (user.tags.Contains("system_trust_veteran") && user.tags.Contains("system_trust_trusted"))
                return TrustRanks.Trusted;
            if (user.tags.Contains("system_trust_trusted") && user.tags.Contains("system_trust_known"))
                return TrustRanks.Known;
            return TrustRanks.Ignore;
        }

        private enum TrustRanks
        {
            Ignore,
            Known,
            Trusted,
            Veteran,
            Legendary,
        }

        private static Player GetPlayerByUserId(string userId)
        {
            foreach (var player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
                if (player.prop_APIUser_0 != null && player.prop_APIUser_0.id == userId)
                    return player;
            return null;
        }
    }
}
