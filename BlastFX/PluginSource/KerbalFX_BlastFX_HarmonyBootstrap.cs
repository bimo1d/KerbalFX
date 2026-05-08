using System;

namespace KerbalFX.BlastFX
{
    internal static class BlastFxHarmonyBootstrap
    {
        private static bool patched;

        internal static void ApplyPatchesOnce()
        {
            if (patched)
                return;

            try
            {
                var harmony = new HarmonyLib.Harmony("KerbalFX.BlastFX");
                harmony.PatchAll(typeof(BlastFxHarmonyBootstrap).Assembly);
                patched = true;
            }
            catch (Exception ex)
            {
                BlastFxLog.Info(KSP.Localization.Localizer.Format(
                    BlastFxLoc.LogHarmonyPatchFailed, ex.Message));
                BlastFxLog.DebugException("Harmony-BlastFX-bootstrap", ex);
            }
        }
    }
}
