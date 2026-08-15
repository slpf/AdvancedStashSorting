using System.Collections.Generic;
using System.Reflection;
using AdvancedStashSorting.UI;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace AdvancedStashSorting.Patches;

public class TextInputCommandPatch : ModulePatch
{
    private static readonly List<ECommand> SuppressedCommands = [];

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(InputNode), nameof(InputNode.TranslateInput));
    }

    [PatchPrefix]
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(ref List<ECommand> commands)
    {
        if (!RarityColorRow.IsInputFocused) return;

        SuppressedCommands.Clear();
        commands = SuppressedCommands;
    }
}