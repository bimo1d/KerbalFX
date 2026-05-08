using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal partial class BlastFxController
    {
        private const float VacuumDebrisAnchorRetargetWindow = 0.75f;

        private static Transform ResolveVacuumDebrisAnchor(Part part)
        {
            if (part == null) return null;
            if (part.transform != null) return part.transform;
            if (part.vessel != null && part.vessel.transform != null)
                return part.vessel.transform;
            return null;
        }

        private void LateUpdate()
        {
            if (activePoolSlotCount <= 0) return;

            for (int s = 0; s < vacuumSlots.Length; s++)
            {
                List<PoolSlot> slots = vacuumSlots[s];
                if (slots == null) continue;
                for (int i = 0; i < slots.Count; i++)
                {
                    PoolSlot slot = slots[i];
                    if (slot == null) continue;
                    if (!slot.Busy) continue;
                    if (!slot.VacuumDebrisFromBundledPrefab) continue;

                    Transform anchor = slot.AnchorTransform;
                    if (anchor == null)
                    {
                        slot.AnchorTransform = null;
                        continue;
                    }

                    if (slot.Root != null)
                        slot.Root.transform.position = anchor.position;
                }
            }
        }

        private void RetargetRecentVacuumDebrisAnchors(Vessel fromVessel, Vessel toVessel)
        {
            if (fromVessel == null || toVessel == null) return;
            Vessel active = FlightGlobals.ActiveVessel;
            if (active == null) return;

            Vessel debris = active.id == fromVessel.id ? toVessel
                          : active.id == toVessel.id   ? fromVessel
                          : null;
            if (debris == null || debris.rootPart == null
                || debris.rootPart.transform == null) return;

            Transform debrisAnchor = debris.rootPart.transform;
            float now = Time.time;
            int retargeted = 0;

            for (int s = 0; s < vacuumSlots.Length; s++)
            {
                List<PoolSlot> slots = vacuumSlots[s];
                if (slots == null) continue;
                for (int i = 0; i < slots.Count; i++)
                {
                    PoolSlot slot = slots[i];
                    if (slot == null) continue;
                    if (!slot.Busy) continue;
                    if (!slot.VacuumDebrisFromBundledPrefab) continue;

                    float spawnTime = slot.ReturnTime - VacuumDebrisReturnDelay;
                    if (now - spawnTime > VacuumDebrisAnchorRetargetWindow) continue;

                    slot.AnchorTransform = debrisAnchor;
                    retargeted++;
                }
            }

            if (retargeted > 0 && BlastFxConfig.DebugLogging)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogVacuumAnchorRetarget,
                    retargeted,
                    debris.vesselName,
                    debris.rootPart.partInfo != null ? debris.rootPart.partInfo.name : "?"));
            }
        }
    }
}
