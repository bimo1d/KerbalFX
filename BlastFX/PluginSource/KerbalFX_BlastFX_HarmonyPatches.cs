using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    [HarmonyPatch(typeof(FXMonger))]
    internal static class BlastFxFxMongerExplodeVacuumPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(FXMonger.Explode))]
        [HarmonyPatch(new[] { typeof(Part), typeof(Vector3d), typeof(double) })]
        private static bool PrefixExplodeSkipInVacuumReplacement(Part source, Vector3d blastPos,
            double howhard)
        {
            return !BlastFxController.TrySuppressStockFxMongerExplode(source);
        }
    }

    [HarmonyPatch]
    internal static class BlastFxKopernicusLogAggregatorOnCrashPatch
    {
        private const string TargetTypeName = "Kopernicus.RuntimeUtility.LogAggregatorWorker";
        private const string TargetMethodName = "AggregateLogs";

        private static MethodBase ResolveTarget()
        {
            Type type = AccessTools.TypeByName(TargetTypeName);
            if (type == null)
                return null;

            return AccessTools.Method(type, TargetMethodName, new[] { typeof(EventReport) });
        }

        private static bool Prepare()
        {
            return ResolveTarget() != null;
        }

        private static MethodBase TargetMethod()
        {
            return ResolveTarget();
        }

        private static bool Prefix(EventReport report)
        {
            return report == null;
        }
    }

    [HarmonyPatch]
    internal static class BlastFxRseShipEffectsDebrisSkipPatch
    {
        private const string TargetTypeName = "RocketSoundEnhancement.ShipEffects";
        private const string TargetMethodName = "Initialize";

        private static MethodBase ResolveTarget()
        {
            Type type = AccessTools.TypeByName(TargetTypeName);
            if (type == null)
                return null;

            return AccessTools.Method(type, TargetMethodName, Type.EmptyTypes);
        }

        private static bool Prepare()
        {
            return ResolveTarget() != null;
        }

        private static MethodBase TargetMethod()
        {
            return ResolveTarget();
        }

        private static bool Prefix(VesselModule __instance, ref bool __result)
        {
            Vessel v = Traverse.Create(__instance).Field("vessel").GetValue<Vessel>();
            if (v == null)
                return true;

            if (v.Parts == null || v.Parts.Count <= 1)
                return true;

            if (v.vesselType != VesselType.Debris &&
                v.vesselType != VesselType.SpaceObject &&
                v.vesselType != VesselType.DroppedPart)
                return true;

            Traverse.Create(__instance).Field("ignoreVessel").SetValue(true);
            __result = true;
            return false;
        }
    }
}
