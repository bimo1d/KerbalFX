using System.Collections.Generic;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal static class TouchdownBurstPool
    {
        internal sealed class Slot
        {
            public GameObject Root;
            public ParticleSystem RingEdge;
            public ParticleSystem RingMist;
            public bool Busy;
            public float ReturnTime;
        }

        private static readonly List<Slot> slots = new List<Slot>(4);
        private const float SlotReturnPadSeconds = 0.90f;

        public static Slot Acquire(int layer)
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                Slot existing = slots[i];
                if (existing == null || existing.Root == null)
                {
                    slots.RemoveAt(i);
                    continue;
                }
                if (existing.Busy)
                    continue;

                SetSlotLayer(existing, layer);
                return existing;
            }

            Slot fresh = BuildSlot(layer);
            if (fresh != null)
                slots.Add(fresh);
            return fresh;
        }

        public static void MarkBusy(Slot slot, float visualLifetime)
        {
            if (slot == null)
                return;
            slot.Busy = true;
            slot.ReturnTime = Time.time + Mathf.Max(0.05f, visualLifetime) + SlotReturnPadSeconds;
        }

        public static void ReturnExpired()
        {
            if (slots.Count == 0)
                return;

            float now = Time.time;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                Slot slot = slots[i];
                if (slot == null || slot.Root == null)
                {
                    slots.RemoveAt(i);
                    continue;
                }
                if (!slot.Busy || now < slot.ReturnTime)
                    continue;

                StopSlotImmediate(slot);
                slot.Busy = false;
            }
        }

        private static void StopSlotImmediate(Slot slot)
        {
            if (slot.RingEdge != null)
                slot.RingEdge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (slot.RingMist != null)
                slot.RingMist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (slot.Root != null)
                slot.Root.SetActive(false);
        }

        private static void SetSlotLayer(Slot slot, int layer)
        {
            if (slot.Root != null)
                slot.Root.layer = layer;
            if (slot.RingEdge != null)
                slot.RingEdge.gameObject.layer = layer;
            if (slot.RingMist != null)
                slot.RingMist.gameObject.layer = layer;
        }

        private static Slot BuildSlot(int layer)
        {
            GameObject root = new GameObject("KerbalFX_ImpactRingShockPool");
            root.SetActive(false);
            root.layer = layer;

            ParticleSystem ringEdge = TouchdownBurstEmitter.BuildPooledTouchdownLayer(root.transform, layer, "RingEdge");
            ParticleSystem ringMist = TouchdownBurstEmitter.BuildPooledTouchdownLayer(root.transform, layer, "RingMist");
            if (ringEdge == null || ringMist == null)
            {
                Object.Destroy(root);
                return null;
            }

            return new Slot
            {
                Root = root,
                RingEdge = ringEdge,
                RingMist = ringMist,
                Busy = false,
                ReturnTime = 0f
            };
        }
    }
}
