namespace Devian
{
    /// <summary>
    /// Common effect player interface.
    /// Public but CommonEffectObject-internal usage only -> underscore prefix.
    /// SSOT: skills/devian-unity/30-unity-components/22-common-effect-manager/SKILL.md
    /// </summary>
    public interface ICommonEffectPlayer
    {
        void _OnEffectAwake(CommonEffectObject owner);
        void _OnEffectPlay();
        void _OnEffectPause();
        void _OnEffectResume();
        void _OnEffectStop();
        void _OnEffectLateUpdate();
        void _OnEffectClear();
        void _SetSortingOrder(int order);
    }
}
