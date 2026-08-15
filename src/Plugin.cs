using System.IO;
using System.Reflection;
using AdvancedStashSorting;
using AdvancedStashSorting.Patches;
using BepInEx;
using BepInEx.Logging;
using EFT;
using HarmonyLib;

[assembly: AssemblyProduct(ModInfo.Name)]
[assembly: AssemblyTitle(ModInfo.Name)]
[assembly: AssemblyDescription(ModInfo.Description)]
[assembly: AssemblyCopyright(ModInfo.Copyright)]
[assembly: AssemblyVersion(ModInfo.Version)]
[assembly: AssemblyFileVersion(ModInfo.Version)]
[assembly: AssemblyInformationalVersion(ModInfo.Version)]

namespace AdvancedStashSorting;

[BepInPlugin(ModInfo.Guid, ModInfo.ClientName, ModInfo.Version)]
[BepInDependency("com.tyfon.uifixes", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public static ManualLogSource LogSource;

    private void Awake()
    {
        LogSource = Logger;

        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string confPath = Path.Combine(pluginDir, "AdvancedStashSorting.json");

        AdvancedStashSorting.Config.Load(confPath);
        
        LocaleManagerClass lm = LocaleManagerClass.LocaleManagerClass;
        Localization.Culture = lm.String_0;
        lm.AddLocaleUpdateListener(() => Localization.Culture = lm.String_0);

        Localization.LoadLocales(pluginDir);

        new SortButtonPatch().Enable();
        new ItemSorterCriteriaPatch().Enable();
        new SortPreparationPatch().Enable();
        new TextInputCommandPatch().Enable();
        EditTagWindowPatch.Enable();

        if (AccessTools.TypeByName("UIFixes.SortPatches+StackFirstPatch") != null) new UIFixesCompatPatch().Enable();
    }
}