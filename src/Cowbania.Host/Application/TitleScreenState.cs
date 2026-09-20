namespace Cowbania.Host.Application;

internal sealed class TitleScreenState
{
    private bool initialized;
    private bool enterWasDown;

    public bool HasStarted { get; private set; }

    public void Update(bool enterIsDown)
    {
        if (!initialized)
        {
            initialized = true;
            enterWasDown = enterIsDown;
            return;
        }
        if (!HasStarted && enterIsDown && !enterWasDown)
            HasStarted = true;
        enterWasDown = enterIsDown;
    }
}