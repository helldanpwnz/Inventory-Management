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
                }
            }

            // === 2. ПАТЧ ДЛЯ NICE INVENTORY TAB ===
         //  var nitEquippedType = AccessTools.TypeByName("NiceInventoryTab.EquippedItem");
         //   if (nitEquippedType != null)
         //   {
         //       var drawMethod = AccessTools.Method(nitEquippedType, "Draw");
          //      if (drawMethod != null)
          //          harmony.Patch(drawMethod, 
         //               prefix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Prefix_DrawItem)),
          //              postfix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Postfix_DrawItem)));
         //   }

            var nitInvType = AccessTools.TypeByName("NiceInventoryTab.InventoryItem");
            if (nitInvType != null)
            {
                var drawMethod = AccessTools.Method(nitInvType, "Draw");
                if (drawMethod != null)
                    harmony.Patch(drawMethod, 
                        prefix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Prefix_DrawItem)),
                        postfix: new HarmonyMethod(typeof(Patch_NiceInventoryTab), nameof(Patch_NiceInventoryTab.Postfix_DrawItem)));
            }
        }
    }

    public static class Patch_NiceInventoryTab
    {
        // Кэшируем доступ к полям чужого мода. object — тип владельца, Thing/Rect — типы полей.
        private static AccessTools.FieldRef<object, Thing> itemField;
        private static AccessTools.FieldRef<object, Rect> geometryField;

        private static void InitFields(object instance)
        {
            if (itemField == null) itemField = AccessTools.FieldRefAccess<Thing>(instance.GetType(), "Item");
            if (geometryField == null) geometryField = AccessTools.FieldRefAccess<Rect>(instance.GetType(), "Geometry");
        }

        // 1. ПРЕФИКС: Перехватываем клик ДО того, как основной мод создаст свою кнопку
        public static void Prefix_DrawItem(object __instance)
        {
            // Нас интересует только левый клик мыши
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0) return;

            InitFields(__instance);
            Thing item = itemField(__instance);
            Rect rect = geometryField(__instance);
            
            if (item == null) return;

            float currentOffset = 8f; 
            float rowY = rect.y + 8f; 

            // Проверяем клик по замку склада
            if (QuickUnloadMod.settings.showStorageLock)
            {
                Rect rectStorage = new Rect(rect.x + currentOffset, rowY, 20f, 20f);
                if (Mouse.IsOver(rectStorage))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    if (QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber)) 
                        QuickUnloadGameComp.lockedStorage.Remove(item.thingIDNumber);
                    else 
                        QuickUnloadGameComp.lockedStorage.Add(item.thingIDNumber);
                    
                    Event.current.Use(); // ПОГЛОЩАЕМ КЛИК!
                    return;
                }
                currentOffset += 22f; 
            }

            // Проверяем клик по замку еды
            if (QuickUnloadMod.settings.showConsumeLock)
            {
                Rect rectConsume = new Rect(rect.x + currentOffset, rowY, 20f, 20f);
                if (Mouse.IsOver(rectConsume))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    if (QuickUnloadGameComp.lockedConsume.Contains(item.thingIDNumber)) 
                        QuickUnloadGameComp.lockedConsume.Remove(item.thingIDNumber);
                    else 
                        QuickUnloadGameComp.lockedConsume.Add(item.thingIDNumber);
                    
                    Event.current.Use(); // ПОГЛОЩАЕМ КЛИК!
                }
            }
        }

        // 2. ПОСТФИКС: Только рисуем иконки поверх всего остального
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
                Color hoverColor = storageLocked ? new Color(1f, 1f, 0.5f) : GenUI.MouseoverColor;
                
                GUI.color = Mouse.IsOver(rectStorage) ? hoverColor : baseColor;
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
                Color hoverColor = consumeLocked ? new Color(1f, 1f, 0.5f) : GenUI.MouseoverColor;
                
                GUI.color = Mouse.IsOver(rectConsume) ? hoverColor : baseColor;
                GUI.DrawTexture(rectConsume, QU_Textures.IconConsume);
                GUI.color = Color.white;
            }
        }
    }
}