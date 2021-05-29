using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using VRC;
using VRC.Core;
using MelonLoader;
using Harmony;
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
        public const string Version = "1.1.1";
        public const string DownloadLink = "https://github.com/RequiDev/OGTrustRanks";
    }

    public class OGTrustRanks : MelonMod
    {
        private static readonly PropertyInfo VRCPlayer_ModTag = null;
        private static Color _trustedUserColor;
        private static Color _veteranUserColor;
        private static Color _legendaryUserColor;
        private static MelonPreferences_Entry<bool> _enabledPref;
        private static MelonPreferences_Entry<int> _veteranColorRPref;
        private static MelonPreferences_Entry<int> _veteranColorGPref;
        private static MelonPreferences_Entry<int> _veteranColorBPref;
        private static MelonPreferences_Entry<int> _legendaryColorRPref;
        private static MelonPreferences_Entry<int> _legendaryColorGPref;
        private static MelonPreferences_Entry<int> _legendaryColorBPref;
        private static readonly List<APIUser> CachedApiUsers = new List<APIUser>();
        private static readonly Queue<string> UsersToFetch = new Queue<string>();
        private static readonly Random Random = new Random();
        private static MethodBase _showSocialRankMethod;

        public override void OnApplicationStart()
        {
            var cat = MelonPreferences.CreateCategory("ogtrustranks", "OGTrustRanks");
            _enabledPref = (MelonPreferences_Entry<bool>)cat.CreateEntry("enabled", true, "Enabled");
            _veteranColorRPref = (MelonPreferences_Entry<int>)cat.CreateEntry("VeteranColorR", 171, "Red component of the Veteran color");
            _veteranColorGPref = (MelonPreferences_Entry<int>)cat.CreateEntry("VeteranColorG", 205, "Green component of the Veteran color");
            _veteranColorBPref = (MelonPreferences_Entry<int>)cat.CreateEntry("VeteranColorB", 239, "Blue component of the Veteran color");
            _legendaryColorRPref = (MelonPreferences_Entry<int>)cat.CreateEntry("LegendaryColorR", 255, "Red component of the Legendary color");
            _legendaryColorGPref = (MelonPreferences_Entry<int>)cat.CreateEntry("LegendaryColorG", 105, "Green component of the Legendary color");
            _legendaryColorBPref = (MelonPreferences_Entry<int>)cat.CreateEntry("LegendaryColorB", 180, "Blue component of the Legendary color");

            _trustedUserColor = new Color(0.5058824f, 0.2627451f, 0.9019608f);
            UpdateColors();

            var friendlyNameTargetMethod = typeof(VRCPlayer).GetMethods().FirstOrDefault(it => !it.Name.Contains("PDM") && it.ReturnType.ToString().Equals("System.String") && it.GetParameters().Length == 1 && it.GetParameters()[0].ParameterType.ToString().Equals("VRC.Core.APIUser"));
            Harmony.Patch(friendlyNameTargetMethod, new HarmonyMethod(typeof(OGTrustRanks).GetMethod(nameof(GetFriendlyDetailedNameForSocialRank), BindingFlags.NonPublic | BindingFlags.Static)));

            var colorForRankTargetMethods = typeof(VRCPlayer).GetMethods().Where(it => it.ReturnType.ToString().Equals("UnityEngine.Color") && it.GetParameters().Length == 1 && it.GetParameters()[0].ParameterType.ToString().Equals("VRC.Core.APIUser")).ToList();
            colorForRankTargetMethods.ForEach(it =>
                Harmony.Patch(it, new HarmonyMethod(typeof(OGTrustRanks).GetMethod(nameof(GetColorForSocialRank), BindingFlags.NonPublic | BindingFlags.Static)))
            );

            _showSocialRankMethod = XrefScanner.XrefScan(friendlyNameTargetMethod).Single(x =>
            {
                if (x.Type != XrefType.Method)
                    return false;

                var m = x.TryResolve();
                if (m == null)
                    return false;

                if (!m.IsStatic)
                    return false;

                if (m.DeclaringType != typeof(VRCPlayer))
                    return false;

                var asInfo = m as MethodInfo;
                if (asInfo == null)
                    return false;

                if (asInfo.ReturnType != typeof(bool))
                    return false;

                if (m.GetParameters().Length != 1 && m.GetParameters()[0].ParameterType != typeof(APIUser))
                    return false;

                return XrefScanner.XrefScan(m).Count() > 1;
            }).TryResolve();
            

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
            var apiUser = player.field_Private_APIUser_0;
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
            SetupTrustRankButton();
        }

        private static void UpdateColors()
        {
            _veteranUserColor = new Color(_veteranColorRPref.Value / 255.0f, _veteranColorGPref.Value / 255.0f, _veteranColorBPref.Value / 255.0f);
            _legendaryUserColor = new Color(_legendaryColorRPref.Value / 255.0f, _legendaryColorGPref.Value / 255.0f, _legendaryColorBPref.Value / 255.0f);
        }

        private static void SetupTrustRankButton()
        {
            if (QuickMenu.prop_QuickMenu_0 == null)
                return;
            var quickMenuGameObject = QuickMenu.prop_QuickMenu_0.field_Private_GameObject_4;
            if (quickMenuGameObject == null)
                return;
            var component = quickMenuGameObject.transform.Find("Toggle_States_ShowTrustRank_Colors").GetComponent<UiToggleButton>();
            if (component == null)
                return;
            if (_enabledPref.Value)
            {
                var rank = GetTrustRankEnum(APIUser.CurrentUser);
                switch (rank)
                {
                    case TrustRanks.Veteran:
                        SetupRankDisplay(component, "Veteran User", _veteranUserColor);
                        break;
                    case TrustRanks.Legendary:
                        SetupRankDisplay(component, "Legendary User", _legendaryUserColor);
                        break;
                    case TrustRanks.Ignore:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
                SetupRankDisplay(component, "Trusted User", _trustedUserColor);
        }

        private static void SetupRankDisplay(UiToggleButton toggleButton, string display_name, Color color)
        {
            var displayTransform = toggleButton.transform.Find("TRUSTED");
            if (displayTransform == null)
                return;
            var gameObject = displayTransform.gameObject;
            if ((gameObject == null) || (gameObject.gameObject == null))
                return;
            toggleButton.field_Public_GameObject_0 = gameObject.transform.Find("ON").gameObject;
            Text[] btnTextsOn = toggleButton.field_Public_GameObject_0.GetComponentsInChildren<Text>();
            btnTextsOn[3].text = display_name;
            btnTextsOn[3].color = color;
            toggleButton.field_Public_GameObject_1 = gameObject.transform.Find("OFF").gameObject;
            Text[] btnTextsOff = toggleButton.field_Public_GameObject_1.GetComponentsInChildren<Text>();
            btnTextsOff[3].text = display_name;
            btnTextsOff[3].color = color;
        }

        private static bool GetFriendlyDetailedNameForSocialRank(APIUser __0, ref string __result)
        {
            if (__0 == null || !_enabledPref.Value) return true;

            var player = GetUserById(__0.id);
            if (player == null)
                return true;

            var showSocialRank = (bool)_showSocialRankMethod.Invoke(null, new object[] { __0 });
            if (!showSocialRank) return true;
            if (!__0.hasVIPAccess || (__0.hasModerationPowers && ((!(null != player._vrcplayer) ? !__0.showModTag : string.IsNullOrEmpty((string)VRCPlayer_ModTag.GetGetMethod().Invoke(player._vrcplayer, null))))))
            {
                var apiUser = CachedApiUsers.Find(x => x.id == __0.id) ?? __0;
                var rank = GetTrustRankEnum(apiUser);
                switch (rank)
                {
                    case TrustRanks.Legendary:
                        __result = "Legendary User";
                        return false;
                    case TrustRanks.Veteran:
                        __result = "Veteran User";
                        return false;
                    case TrustRanks.Ignore:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return true;
        }

        private static bool GetColorForSocialRank(APIUser __0, ref Color __result)
        {
            if (__0 == null || !_enabledPref.Value || APIUser.IsFriendsWith(__0.id)) return true;

            var player = GetUserById(__0.id);
            if (player == null)
                return true;

            var showSocialRank = (bool)_showSocialRankMethod.Invoke(null, new object[] {__0});
            if (!showSocialRank) return true;

            if (!__0.hasVIPAccess || (__0.hasModerationPowers && ((!(null != player._vrcplayer) ? !__0.showModTag : string.IsNullOrEmpty((string)VRCPlayer_ModTag.GetGetMethod().Invoke(player._vrcplayer, null))))))
            {
                var apiUser = CachedApiUsers.Find(x => x.id == __0.id) ?? __0;
                var rank = GetTrustRankEnum(apiUser);
                switch (rank)
                {
                    case TrustRanks.Legendary:
                        __result = _legendaryUserColor;
                        return false;
                    case TrustRanks.Veteran:
                        __result = _veteranUserColor;
                        return false;
                    case TrustRanks.Ignore:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return true;
        }

        private static TrustRanks GetTrustRankEnum(APIUser user)
        {
            if (user?.tags == null || (user.tags.Count <= 0))
                return TrustRanks.Ignore;
            if (user.tags.Contains("system_legend") && user.tags.Contains("system_trust_legend") && user.tags.Contains("system_trust_trusted"))
                return TrustRanks.Legendary;
            if (user.tags.Contains("system_trust_legend") && user.tags.Contains("system_trust_trusted"))
                return TrustRanks.Veteran;
            return TrustRanks.Ignore;
        }

        private enum TrustRanks
        {
            Ignore,
            Veteran,
            Legendary
        }

        private static Player GetUserById(string userId)
        {
            foreach (var ply in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
                if ((ply.prop_APIUser_0 != null) && (ply.prop_APIUser_0.id == userId))
                    return ply;
            return null;
        }
    }
}
