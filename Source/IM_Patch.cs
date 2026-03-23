using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.Sound;

namespace InventoryManagement
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("com.helldan.quickunload");
            harmony.PatchAll();

            // === 1. ПАТЧ ДЛЯ RPG INVENTORY (Всех версий) ===
            var rpgTypes = new[] {
                "RPG_Inventory.ITab_Pawn_Gear_RPG", 
                "Sandy_Abyss.ITab_Pawn_Gear_RPG",    
                "Ashen_RPGInventory.ITab_Pawn_Gear_RPG",
                "Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab" 
            };

            foreach (var typeName in rpgTypes)
            {
                var targetType = AccessTools.TypeByName(typeName);
                if (targetType == null) continue;

                var targetMethod = AccessTools.Method(targetType, "DrawThingRow") 
                                ?? AccessTools.Method(targetType, "DrawInventoryRow");

                if (targetMethod != null)
                {
                    harmony.Patch(targetMethod, 
                        prefix: new HarmonyMethod(typeof(Patch_DrawThingRow), nameof(Patch_DrawThingRow.Prefix)),
                        postfix: new HarmonyMethod(typeof(Patch_DrawThingRow), nameof(Patch_DrawThingRow.Postfix)));
                    
                    // ДОБАВЛЯЕМ ПОДДЕРЖКУ ПОДСКАЗОК (DropSome)
                    harmony.Patch(targetMethod, 
                        prefix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.DrawPrefix)),
                        postfix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.DrawPostfix)));
                }

                // Доп-перехват для RPG-сброса (если есть InterfaceDrop)
                var dropMethod = AccessTools.Method(targetType, "InterfaceDrop");
                if (dropMethod != null)
                {
                    harmony.Patch(dropMethod, prefix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.Prefix_InterfaceDrop)));
                }
            }

            // === 2. ПАТЧИ ДЛЯ NICE INVENTORY TAB ===
            var nitInvType = AccessTools.TypeByName("NiceInventoryTab.InventoryItem");
            if (nitInvType != null)
            {
                var drawMethod = AccessTools.Method(nitInvType, "Draw");
                if (drawMethod != null)
                {
                    // Обычные замки
                    harmony.Patch(drawMethod, 
                        prefix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Prefix_DrawItem)),
                        postfix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Postfix_DrawItem)));
                    
                    // Поддержка подсказок (DropSome)
                    harmony.Patch(drawMethod, 
                        prefix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Prefix_DropSome_Tip)),
                        postfix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Postfix_DropSome_Tip)));
                }
            }
            
            var nitEquippedType = AccessTools.TypeByName("NiceInventoryTab.EquippedItem");
            if (nitEquippedType != null)
            {
                var drawMethod = AccessTools.Method(nitEquippedType, "Draw");
                if (drawMethod != null)
                {
                    // Поддержка подсказок (DropSome)
                    harmony.Patch(drawMethod, 
                        prefix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Prefix_DropSome_Tip)),
                        postfix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Postfix_DropSome_Tip)));
                }
            }

            // Статический метод сброса в Nice Inventory
            var nitUtils = AccessTools.TypeByName("NiceInventoryTab.CommandUtility");
            if (nitUtils != null)
            {
                var cmdDrop = AccessTools.Method(nitUtils, "CommandDrop");
                if (cmdDrop != null)
                {
                    harmony.Patch(cmdDrop, prefix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.Prefix_CommandDrop)));
                }
            }

            // === 3. ВАНИЛЬНЫЕ ПАТЧИ ДЛЯ DROP SOME (Shift+Click) ===
            var vanInterfaceDrop = AccessTools.Method(typeof(ITab_Pawn_Gear), "InterfaceDrop");
            if (vanInterfaceDrop != null)
                harmony.Patch(vanInterfaceDrop, prefix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.Prefix_InterfaceDrop)));

            // Патч на подсказки TooltipHandler
            var tipRegionSig = AccessTools.Method(typeof(TooltipHandler), "TipRegion", new[] { typeof(Rect), typeof(TipSignal) });
            if (tipRegionSig != null)
                harmony.Patch(tipRegionSig, prefix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.Prefix_TipRegion)));

            var tipRegionTag = AccessTools.Method(typeof(TooltipHandler), "TipRegion", new[] { typeof(Rect), typeof(TaggedString) });
            if (tipRegionTag != null)
                harmony.Patch(tipRegionTag, prefix: new HarmonyMethod(typeof(Patch_Drop), nameof(Patch_Drop.Prefix_TipRegion_Tagged)));
        }
    }

    public static class Patch_NiceInventoryTab
    {
        private static AccessTools.FieldRef<object, Thing> itemField;
        private static AccessTools.FieldRef<object, Rect> geometryField;

        private static void InitFields(object instance)
        {
            if (itemField == null) itemField = AccessTools.FieldRefAccess<Thing>(instance.GetType(), "Item");
            if (geometryField == null) geometryField = AccessTools.FieldRefAccess<Rect>(instance.GetType(), "Geometry");
        }

        // Вспомогательный патч для тултипов DropSome в Nice Inventory
        // Из-за того что InventoryItem и EquippedItem — разные классы, используем простую рефлексию или передаем тип
        public static void Prefix_DropSome_Tip(object __instance)
        {
            Thing thing = (Thing)AccessTools.Field(__instance.GetType(), "Item")?.GetValue(__instance);
            if (thing != null) Patch_Drop.DrawPrefix(thing);
        }

        public static void Postfix_DropSome_Tip() => Patch_Drop.DrawPostfix();

        // Перехват клика по замкам
        public static void Prefix_DrawItem(object __instance)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0) return;

            InitFields(__instance);
            Thing item = itemField(__instance);
            Rect rect = geometryField(__instance);
            if (item == null) return;

            float currentOffset = 8f; 
            float rowY = rect.y + 8f; 

            if (QuickUnloadMod.settings.showStorageLock)
            {
                Rect rectStorage = new Rect(rect.x + currentOffset, rowY, 20f, 20f);
                if (Mouse.IsOver(rectStorage))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    if (QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber)) QuickUnloadGameComp.lockedStorage.Remove(item.thingIDNumber);
                    else QuickUnloadGameComp.lockedStorage.Add(item.thingIDNumber);
                    Event.current.Use();
                    return;
                }
                currentOffset += 22f; 
            }

            if (QuickUnloadMod.settings.showConsumeLock)
            {
                Rect rectConsume = new Rect(rect.x + currentOffset, rowY, 20f, 20f);
                if (Mouse.IsOver(rectConsume))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    if (QuickUnloadGameComp.lockedConsume.Contains(item.thingIDNumber)) QuickUnloadGameComp.lockedConsume.Remove(item.thingIDNumber);
                    else QuickUnloadGameComp.lockedConsume.Add(item.thingIDNumber);
                    Event.current.Use();
                }
            }
        }

        public static void Postfix_DrawItem(object __instance)
        {
            InitFields(__instance);
            Thing item = itemField(__instance);
            Rect rect = geometryField(__instance);
            if (item == null) return;

            float currentOffset = 8f; 
            float rowY = rect.y + 8f; 

            if (QuickUnloadMod.settings.showStorageLock)
            {
                Rect rectStorage = new Rect(rect.x + currentOffset, rowY, 20f, 20f);
                bool storageLocked = QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber);
                TooltipHandler.TipRegion(rectStorage, "IM.StorageLockTooltip".Translate());
                
                Color baseColor = storageLocked ? Color.yellow : Color.white;
                GUI.color = Mouse.IsOver(rectStorage) ? (storageLocked ? new Color(1f, 1f, 0.5f) : GenUI.MouseoverColor) : baseColor;
                GUI.DrawTexture(rectStorage, QU_Textures.IconStorage);
                GUI.color = Color.white;
                currentOffset += 22f; 
            }

            if (QuickUnloadMod.settings.showConsumeLock)
            {
                Rect rectConsume = new Rect(rect.x + currentOffset, rowY, 20f, 20f);
                bool consumeLocked = QuickUnloadGameComp.lockedConsume.Contains(item.thingIDNumber);
                TooltipHandler.TipRegion(rectConsume, "IM.ConsumeLockTooltip".Translate());
                
                Color baseColor = consumeLocked ? Color.yellow : Color.white;
                GUI.color = Mouse.IsOver(rectConsume) ? (consumeLocked ? new Color(1f, 1f, 0.5f) : GenUI.MouseoverColor) : baseColor;
                GUI.DrawTexture(rectConsume, QU_Textures.IconConsume);
                GUI.color = Color.white;
            }
        }
    }
}