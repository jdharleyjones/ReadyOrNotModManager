using ReadyOrNotModManager.Core.Deployment;

namespace ReadyOrNotModManager.App.Services;

public static class SetupGate
{
    public static bool ShouldShowSetup(LocalSettings settings)
    {
        return settings.ForceSetupWizard ||
            string.IsNullOrWhiteSpace(settings.ApiKey) ||
            !ReadyOrNotPaths.LooksLikeInstallDirectory(settings.ReadyOrNotDirectory);
    }
}
