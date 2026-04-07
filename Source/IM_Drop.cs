using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace InventoryManagement
{
    // === ЛОГИКА ВЫБОРА КОЛИЧЕСТВА ПРИ ВЫБРОСЕ (Shift+Click) ===
    public static class Patch_Drop
    {
        public static bool changeTip = false;

        // Патч на подсказку (ванильный TipSignal)
        public static void Prefix_TipRegion(ref TipSignal tip)
        {
            if (QuickUnloadMod.settings.enableDropCountSlider && changeTip && tip.text != null && tip.text == "DropThing".Translate())
            {
                tip.text = "IM.StackDropTooltip".Translate();
            }
        }

        // Патч на подсказку (TaggedString - используется в Nice Inventory)
        public static void Prefix_TipRegion_Tagged(ref TaggedString text)
        {
            if (QuickUnloadMod.settings.enableDropCountSlider && changeTip && text != null && text == "DropThing".Translate())
            {
                text = "IM.StackDropTooltip".Translate();
            }
        }

        // Универсальный перехват сброса для Nice Inventory (Static метод)
        public static bool Prefix_CommandDrop(Pawn pawn, Thing t)
        {
            if (QuickUnloadMod.settings.enableDropCountSlider && pawn != null && t != null && !t.def.destroyOnDrop && t.stackCount > 1 && Event.current.shift)
            {
                Find.WindowStack.Add(new Dialog_Slider(count => "IM.DropCount".Translate(count, t.LabelNoCount), 1, t.stackCount, count =>
                {
                    GenDrop.TryDropSpawn(t.SplitOff(count), pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                }, t.stackCount));
                return false; // Блокируем оригинальный сброс
            }
            return true;
        }

        // Перехват ванильного сброса (ITab_Pawn_Gear.InterfaceDrop)
        public static bool Prefix_InterfaceDrop(Thing t, object __instance)
        {
            if (QuickUnloadMod.settings.enableDropCountSlider && t != null && !t.def.destroyOnDrop && t.stackCount > 1 && Event.current.shift)
            {
                // Пытаемся получить пешку через свойство (есть у ITab_Pawn_Gear)
                Pawn pawn = (Pawn)AccessTools.Property(__instance.GetType(), "SelPawnForGear")?.GetValue(__instance, null);
                if (pawn != null)
                {
                    Find.WindowStack.Add(new Dialog_Slider(count => "IM.DropCount".Translate(count, t.LabelNoCount), 1, t.stackCount, count =>
                    {
                        GenDrop.TryDropSpawn(t.SplitOff(count), pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                    }, t.stackCount));
                    return false;
                }
            }
            return true;
        }

        // Управление флагом тултипа при отрисовке строк (для RPG Inventory и Nice Inventory)
        public static void DrawPrefix(Thing thing)
        {
            if (QuickUnloadMod.settings.enableDropCountSlider && thing != null)
            {
                changeTip = !thing.def.destroyOnDrop && thing.stackCount > 1;
            }
        }

        public static void DrawPostfix()
        {
            changeTip = false;
        }
    }
}
