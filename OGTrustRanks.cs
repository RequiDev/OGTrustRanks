using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnhollowerRuntimeLib.XrefScans;
using UnityEngine;
using UnityEngine.UI;
using VRC;
using VRC.Core;
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
        private static Color _legendColor;
        private static Color _veteranUserColor;
        private static Color _trustedUserColor;
        private static Color _knownUserColor;
        private static Color _userUserColor;
        private static Color _newUserColor;
        private static Color _visitorColor;
        private static Color _nuisanceColor;
        private static MelonPreferences_Entry<bool> _enabledPref;
        private static MelonPreferences_Entry<int> _legendColorRPref;
        private static MelonPreferences_Entry<int> _legendColorGPref;
        private static MelonPreferences_Entry<int> _legendColorBPref;
        private static MelonPreferences_Entry<int> _veteranColorRPref;
        private static MelonPreferences_Entry<int> _veteranColorGPref;
        private static MelonPreferences_Entry<int> _veteranColorBPref;
        private static MelonPreferences_Entry<int> _trustedColorRPref;
        private static MelonPreferences_Entry<int> _trustedColorGPref;
        private static MelonPreferences_Entry<int> _trustedColorBPref;
        private static MelonPreferences_Entry<int> _knownColorRPref;
        private static MelonPreferences_Entry<int> _knownColorGPref;
        private static MelonPreferences_Entry<int> _knownColorBPref;
        private static MelonPreferences_Entry<int> _userColorRPref;
        private static MelonPreferences_Entry<int> _userColorGPref;
        private static MelonPreferences_Entry<int> _userColorBPref;
        private static MelonPreferences_Entry<int> _newUserColorRPref;
        private static MelonPreferences_Entry<int> _newUserColorGPref;
        private static MelonPreferences_Entry<int> _newUserColorBPref;
        private static MelonPreferences_Entry<int> _visitorColorRPref;
        private static MelonPreferences_Entry<int> _visitorColorGPref;
        private static MelonPreferences_Entry<int> _visitorColorBPref;
        private static MelonPreferences_Entry<int> _nuisanceColorRPref;
        private static MelonPreferences_Entry<int> _nuisanceColorGPref;
        private static MelonPreferences_Entry<int> _nuisanceColorBPref;
        private static MelonPreferences_Entry<bool> _reloadAvatar;
        private static readonly List<APIUser> CachedApiUsers = new();
        private static readonly Queue<string> UsersToFetch = new();
        private static readonly Random Random = new();

        private static MethodBase _showSocialRankMethod;
        private static MethodInfo _reloadAvatarMethod;

        public override void OnApplicationStart()
        {
            var cat = MelonPreferences.CreateCategory("ogtrustranks", "OGTrustRanks");
            _enabledPref = cat.CreateEntry("enabled", true, "Enabled");

            _legendColorRPref = cat.CreateEntry("LegendColorR", 255, "Red component of the Legend color");
            _legendColorGPref = cat.CreateEntry("LegendColorG", 105, "Green component of the Legend color");
            _legendColorBPref = cat.CreateEntry("LegendColorB", 180, "Blue component of the Legend color");

            _veteranColorRPref = cat.CreateEntry("VeteranColorR", 255, "Red component of the Veteran user color");
            _veteranColorGPref = cat.CreateEntry("VeteranColorG", 208, "Green component of the Veteran user color");
            _veteranColorBPref = cat.CreateEntry("VeteranColorB", 0, "Blue component of the Veteran color");

            _trustedColorRPref = cat.CreateEntry("TrustedColorR", 130, "Red component of the Trusted user color");
            _trustedColorGPref = cat.CreateEntry("TrustedColorG", 66, "Green component of the Trusted user color");
            _trustedColorBPref = cat.CreateEntry("TrustedColorB", 230, "Blue component of the Trusted user color");

            _knownColorRPref = cat.CreateEntry("KnownColorR", 255, "Red component of the Known user color");
            _knownColorGPref = cat.CreateEntry("KnownColorG", 123, "Green component of the Known user color");
            _knownColorBPref = cat.CreateEntry("KnownColorB", 66, "Blue component of the Known user color");

            _userColorRPref = cat.CreateEntry("UserColorR", 43, "Red component of the User color");
            _userColorGPref = cat.CreateEntry("UserColorG", 207, "Green component of the User color");
            _userColorBPref = cat.CreateEntry("UserColorB", 97, "Blue component of the User color");

            _newUserColorRPref = cat.CreateEntry("NewUserColorR", 23, "Red component of the New user color");
            _newUserColorGPref = cat.CreateEntry("NewUserColorG", 120, "Green component of the New user color");
            _newUserColorBPref = cat.CreateEntry("NewUserColorB", 255, "Blue component of the New user color");

            _visitorColorRPref = cat.CreateEntry("VisitorColorR", 204, "Red component of the Visitor color");
            _visitorColorGPref = cat.CreateEntry("VisitorColorG", 204, "Green component of the Visitor color");
            _visitorColorBPref = cat.CreateEntry("VisitorColorB", 204, "Blue component of the Visitor color");

            _nuisanceColorRPref = cat.CreateEntry("NuisanceColorR", 120, "Red component of the Nuisance color");
            _nuisanceColorGPref = cat.CreateEntry("NuisanceColorG", 47, "Green component of the Nuisance color");
            _nuisanceColorBPref = cat.CreateEntry("NuisanceColorB", 47, "Blue component of the Nuisance color");

            _reloadAvatar = cat.CreateEntry("ReloadAvatar", false,
                "Reload avatars when fetched rank to update colors for BTKANameplateMod");

            UpdateColors();

            var harmony = new HarmonyLib.Harmony("OGTrustRanks");

            var friendlyNameTargetMethod = typeof(VRCPlayer).GetMethods().FirstOrDefault(it =>
                !it.Name.Contains("PDM") && it.ReturnType.ToString().Equals("System.String") &&
                it.GetParameters().Length == 1 &&
                it.GetParameters()[0].ParameterType.ToString().Equals("VRC.Core.APIUser"));
            harmony.Patch(friendlyNameTargetMethod,
                new HarmonyMethod(typeof(OGTrustRanks).GetMethod(nameof(GetFriendlyDetailedNameForSocialRank),
                    BindingFlags.NonPublic | BindingFlags.Static)));

            var colorForRankTargetMethods = typeof(VRCPlayer).GetMethods().Where(it =>
                it.ReturnType.ToString().Equals("UnityEngine.Color") && it.GetParameters().Length == 1 &&
                it.GetParameters()[0].ParameterType.ToString().Equals("VRC.Core.APIUser")).ToList();
            colorForRankTargetMethods.ForEach(it =>
                harmony.Patch(it,
                    new HarmonyMethod(typeof(OGTrustRanks).GetMethod(nameof(GetColorForSocialRank),
                        BindingFlags.NonPublic | BindingFlags.Static)))
            );

            _showSocialRankMethod = XrefScanner.XrefScan(friendlyNameTargetMethod).Single(x =>
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
            }).TryResolve();

            // Thanks loukylor
            _reloadAvatarMethod = typeof(VRCPlayer).GetMethods().First(mi =>
                mi.Name.StartsWith("Method_Private_Void_Boolean_") && mi.Name.Length < 31 &&
                mi.GetParameters().Any(pi => pi.IsOptional));


            MelonCoroutines.Start(InitializeNetworkHooks());
            MelonCoroutines.Start(FetchApiUsers());
        }

        private IEnumerator InitializeNetworkHooks()
        {
            while (NetworkManager.field_Internal_Static_NetworkManager_0 is null) yield return null;
            while (VRCAudioManager.field_Private_Static_VRCAudioManager_0 is null) yield return null;
            while (VRCUiManager.prop_VRCUiManager_0 is null) yield return null;

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

        private static IEnumerator FetchApiUsers()
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
                        if (!_reloadAvatar.Value) return;
                        var player = GetPlayerByUserId(id);
                        _reloadAvatarMethod.Invoke(player._vrcplayer, new object[] {true});
                    }), new Action<string>(_ => { MelonLogger.Error($"Could not fetch APIUser object of {id}"); }));
                    yield return new WaitForSeconds(Random.Next(2, 5));
                }
            }
        }

        public override void OnPreferencesSaved()
        {
            Refresh();
        }

        public override void OnPreferencesLoaded()
        {
            Refresh();
        }

        public override void OnSceneWasInitialized(int buildindex, string name)
        {
            Refresh();
        }

        private static void Refresh()
        {
            UpdateColors();
            // SetupTrustRankButton(); requi fix plz
        }

        private static void UpdateColors()
        {
            _legendColor = new Color(_legendColorRPref.Value / 255.0f, _legendColorGPref.Value / 255.0f,
                _legendColorBPref.Value / 255.0f);

            _veteranUserColor = new Color(_veteranColorRPref.Value / 255.0f, _veteranColorGPref.Value / 255.0f,
                _veteranColorBPref.Value / 255.0f);

            _trustedUserColor = new Color(_trustedColorRPref.Value / 255.0f, _trustedColorGPref.Value / 255.0f,
                _trustedColorBPref.Value / 255.0f);

            _knownUserColor = new Color(_knownColorRPref.Value / 255.0f, _knownColorGPref.Value / 255.0f,
                _knownColorBPref.Value / 255.0f);

            _userUserColor = new Color(_userColorRPref.Value / 255.0f, _userColorGPref.Value / 255.0f,
                _userColorBPref.Value / 255.0f);

            _newUserColor = new Color(_newUserColorRPref.Value / 255.0f, _newUserColorGPref.Value / 255.0f,
                _newUserColorBPref.Value / 255.0f);

            _visitorColor = new Color(_visitorColorRPref.Value / 255.0f, _visitorColorGPref.Value / 255.0f,
                _visitorColorBPref.Value / 255.0f);

            _nuisanceColor = new Color(_nuisanceColorRPref.Value / 255.0f, _nuisanceColorGPref.Value / 255.0f,
                _nuisanceColorBPref.Value / 255.0f);
        }

        private static void SetupTrustRankButton()
        {
            if (QuickMenu.prop_QuickMenu_0 == null)
                return;
            var quickMenuGameObject = QuickMenu.prop_QuickMenu_0.field_Private_GameObject_4;
            if (quickMenuGameObject == null)
                return;
            var component = quickMenuGameObject.transform.Find("Toggle_States_ShowTrustRank_Colors")
                .GetComponent<UiToggleButton>();
            if (component == null)
                return;

            var rank = GetTrustRankEnum(APIUser.CurrentUser);

            if (rank > TrustRanks.Known) return;

            if (_enabledPref.Value)
                switch (rank)
                {
                    case TrustRanks.Veteran:
                        SetupRankDisplay(component, "Veteran User", _veteranUserColor);
                        break;
                    case TrustRanks.Known:
                        SetupRankDisplay(component, "Known User", _knownUserColor);
                        break;
                    case TrustRanks.Trusted:
                        SetupRankDisplay(component, "Trusted User", _trustedUserColor);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            else
                SetupRankDisplay(component, rank == TrustRanks.Known ? "Known User" : "Trusted User",
                    rank == TrustRanks.Known ? _knownUserColor : _trustedUserColor);
        }

        private static void SetupRankDisplay(UiToggleButton toggleButton, string displayName, Color color)
        {
            var displayTransform = toggleButton.transform.Find("TRUSTED");
            if (displayTransform == null)
                return;
            var gameObject = displayTransform.gameObject;
            if (gameObject == null)
                return;
            toggleButton.field_Public_GameObject_0 = displayTransform.Find("ON").gameObject;
            Text[] btnTextsOn = toggleButton.field_Public_GameObject_0.GetComponentsInChildren<Text>();
            btnTextsOn[3].text = displayName;
            btnTextsOn[3].color = color;
            toggleButton.field_Public_GameObject_1 = displayTransform.Find("OFF").gameObject;
            Text[] btnTextsOff = toggleButton.field_Public_GameObject_1.GetComponentsInChildren<Text>();
            btnTextsOff[3].text = displayName;
            btnTextsOff[3].color = color;
        }

        private static bool GetFriendlyDetailedNameForSocialRank(APIUser __0, ref string __result)
        {
            if (__0 == null || __0.IsSelf || !_enabledPref.Value) return true;

            if (GetPlayerByUserId(__0.id) != null)
            {
                var showSocialRank = (bool) _showSocialRankMethod.Invoke(null, new object[] {__0});
                if (!showSocialRank) return true;
            }

            var apiUser = CachedApiUsers.Find(x => x.id == __0.id) ?? __0;
            var rank = GetTrustRankEnum(apiUser);

            __result = GetRank(apiUser, rank);
            return false;
        }

        private static string GetRank(APIUser apiUser, TrustRanks rank)
        {
            if (apiUser.HasTag("system_legend"))
                return $"{rank} User + Legend";

            return rank is TrustRanks.User or TrustRanks.Visitor or TrustRanks.Nuisance
                ? rank.ToString()
                : $"{rank} User";
        }


        private static bool GetColorForSocialRank(APIUser __0, ref Color __result)
        {
            if (__0 == null || APIUser.IsFriendsWith(__0.id) || __0.IsSelf || !_enabledPref.Value) return true;

            if (GetPlayerByUserId(__0.id) != null)
            {
                var showSocialRank = (bool) _showSocialRankMethod.Invoke(null, new object[] {__0});
                if (!showSocialRank) return true;
            }

            var apiUser = CachedApiUsers.Find(x => x.id == __0.id) ?? __0;
            var rank = GetTrustRankEnum(apiUser);

            if (apiUser.HasTag("system_legend"))
            {
                __result = _legendColor;
                return false;
            }

            switch (rank)
            {
                case TrustRanks.Veteran:
                    __result = _veteranUserColor;
                    return false;
                case TrustRanks.Trusted:
                    __result = _trustedUserColor;
                    return false;
                case TrustRanks.Known:
                    __result = _knownUserColor;
                    return false;
                case TrustRanks.User:
                    __result = _userUserColor;
                    return false;
                case TrustRanks.New:
                    __result = _newUserColor;
                    return false;
                case TrustRanks.Visitor:
                    __result = _visitorColor;
                    return false;
                case TrustRanks.Nuisance:
                    __result = _nuisanceColor;
                    return false;
                default:
                    __result = _visitorColor;
                    return false;
            }
        }

        private static TrustRanks GetTrustRankEnum(APIUser user)
        {
            if (user.hasLegendTrustLevel)
                return TrustRanks.Veteran;

            if (user.hasVeteranTrustLevel)
                return TrustRanks.Trusted;

            if (user.hasTrustedTrustLevel)
                return TrustRanks.Known;

            if (user.hasKnownTrustLevel)
                return TrustRanks.User;

            if (user.hasBasicTrustLevel)
                return TrustRanks.New;

            if (user.HasTag(string.Empty) && !user.canPublishAvatars)
                return TrustRanks.Visitor;

            if (user.hasNegativeTrustLevel || user.hasVeryNegativeTrustLevel)
                return TrustRanks.Nuisance;

            return TrustRanks.Visitor;
        }

        private static Player GetPlayerByUserId(string userId)
        {
            foreach (var player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
                if (player.prop_APIUser_0 != null && player.prop_APIUser_0.id == userId)
                    return player;
            return null;
        }

        private enum TrustRanks
        {
            Veteran,
            Trusted,
            Known,
            User,
            New,
            Visitor,
            Nuisance
        }
    }
}