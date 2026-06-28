namespace ReadyOrNotModManager.App.Services;

public sealed class AppUpdateRefreshGate
{
    private bool _hasChecked;

    public bool ShouldCheck(bool force)
    {
        if (force)
        {
            _hasChecked = true;
            return true;
        }

        if (_hasChecked)
        {
            return false;
        }

        _hasChecked = true;
        return true;
    }
}
