using MelonLoader;
using System.Reflection;
using BuildInfo = OGTrustRanks.BuildInfo;

[assembly: AssemblyTitle(BuildInfo.Name)]
[assembly: AssemblyCompany(BuildInfo.Company)]
[assembly: AssemblyProduct(BuildInfo.Name)]
[assembly: AssemblyCopyright("Created by " + BuildInfo.Author)]
[assembly: AssemblyTrademark(BuildInfo.Company)]
[assembly: AssemblyVersion(BuildInfo.Version)]
[assembly: AssemblyFileVersion(BuildInfo.Version)]
[assembly:
    MelonInfo(typeof(OGTrustRanks.OGTrustRanks), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author,
        BuildInfo.DownloadLink)]
[assembly: MelonGame("VRChat", "VRChat")]