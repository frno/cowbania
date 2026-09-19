namespace Cowbania.Core.Presentation.Animation;

[Obsolete("Use FrontierAnimationCatalog.")]
public static class PlaceholderAnimationCatalog
{
    public static AnimationClip For(PresentationAnimationState state) =>
        FrontierAnimationCatalog.For(state);
}
