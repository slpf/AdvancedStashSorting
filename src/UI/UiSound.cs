using Comfort.Common;
using EFT.UI;

namespace AdvancedStashSorting.UI;

public static class UiSound
{
    public static void Play(EUISoundType soundType)
    {
        if (Singleton<GUISounds>.Instantiated) Singleton<GUISounds>.Instance.PlayUISound(soundType);
    }
}