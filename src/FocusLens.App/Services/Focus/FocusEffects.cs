using System.Windows;
using FocusLens.Core.Focus;

namespace FocusLens.App.Services.Focus;

/// <summary>
/// Whether the focus surfaces should go easy on the GPU: no animation, no shadow, and a clock
/// that ticks once a minute. Set by Settings > Focus > Visual effects. Automatic reduces while
/// battery saver is on or Windows animations are turned off (Settings > Accessibility >
/// Visual effects), the Windows counterparts of Low Power Mode and Reduce Motion.
/// </summary>
public static class FocusEffects
{
    public static bool IsReduced(FocusSettings settings) => settings.VisualEffects switch
    {
        FocusVisualEffects.Full => false,
        FocusVisualEffects.Minimal => true,
        _ => BatterySaverOn() || !SystemParameters.ClientAreaAnimation,
    };

    private static bool BatterySaverOn()
    {
        try
        {
            return global::Windows.System.Power.PowerManager.EnergySaverStatus == global::Windows.System.Power.EnergySaverStatus.On;
        }
        catch
        {
            return false;
        }
    }
}
