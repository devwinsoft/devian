namespace Devian
{
    /// <summary>
    /// Effect runner interface.
    /// Public but EffectObject-internal usage only -> underscore prefix.
    /// SSOT: skills/devian-unity/30-unity-components/22-effect-manager/SKILL.md
    /// </summary>
    public interface IEffectRunner
    {
        void _OnEffectAwake(EffectObject owner);
        void _OnEffectPlay();
        void _OnEffectPause();
        void _OnEffectResume();
        void _OnEffectStop();
        void _OnEffectLateUpdate();
        void _OnEffectClear();
        void _SetSortingOrder(int order);
    }
}
