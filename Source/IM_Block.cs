using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace InventoryManagement
{

// --- 1. Хранилище замочков (сохраняется в сейв) ---
    public class QuickUnloadGameComp : GameComponent
    {
        public static HashSet<int> lockedStorage = new HashSet<int>();
        public static HashSet<int> lockedConsume = new HashSet<int>();

        public QuickUnloadGameComp(Game game) : base()
        {
            lockedStorage.Clear();
            lockedConsume.Clear();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref lockedStorage, "lockedStorage", LookMode.Value);
            Scribe_Collections.Look(ref lockedConsume, "lockedConsume", LookMode.Value);
            if (lockedStorage == null) lockedStorage = new HashSet<int>();
            if (lockedConsume == null) lockedConsume = new HashSet<int>();
        }
    }

    // --- 2. Рисуем замочки (галочки) в инвентаре пешки ---
// --- Рисуем свои иконки: идеальная ванильная подсветка (без квадратиков) ---
    [HarmonyLib.HarmonyPatch(typeof(RimWorld.ITab_Pawn_Gear), "DrawThingRow")]
    public static class Patch_DrawThingRow
    {
        public static void Prefix(Verse.Thing thing, ref float width, out float __state, bool inventory)
        {
            __state = width; 
            
            // ПРОВЕРКА: Рисуем только для своих
            Pawn selPawn = Find.Selector.SingleSelectedThing as Pawn;
            if (selPawn == null || !selPawn.IsColonistPlayerControlled) return;

            if (inventory)
            {
                float shift = 0f;
                if (QuickUnloadMod.settings.showStorageLock) shift += 24f;
                if (QuickUnloadMod.settings.showConsumeLock) shift += 24f;
                width -= shift; 
            }
        }
		

        public static void Postfix(ref float y, Verse.Thing thing, bool inventory, float __state)
        {
            // ПРОВЕРКА: Рисуем только для своих
            Pawn selPawn = Find.Selector.SingleSelectedThing as Pawn;
            if (selPawn == null || !selPawn.IsColonistPlayerControlled) return;

            if (inventory)
            {
                float rowY = y - 28f; 
                float originalWidth = __state; 
                float currentOffset = 24f; 

                // --- КНОПКА 1: Замок выгрузки ---
                if (QuickUnloadMod.settings.showStorageLock)
                {
                    UnityEngine.Rect rectStorage = new UnityEngine.Rect(originalWidth - currentOffset, rowY + 2f, 24f, 24f);
                    bool storageLocked = QuickUnloadGameComp.lockedStorage.Contains(thing.thingIDNumber);
                    
                    Verse.TooltipHandler.TipRegion(rectStorage, "IM.StorageLockTooltip".Translate());
                    
                    UnityEngine.Color baseColor = storageLocked ? UnityEngine.Color.yellow : UnityEngine.Color.white;
                    UnityEngine.Color hoverColor = storageLocked ? new UnityEngine.Color(1f, 1f, 0.5f) : Verse.GenUI.MouseoverColor;
                    
                    UnityEngine.GUI.color = Verse.Mouse.IsOver(rectStorage) ? hoverColor : baseColor;
                    UnityEngine.GUI.DrawTexture(rectStorage, QU_Textures.IconStorage);
                    UnityEngine.GUI.color = UnityEngine.Color.white; 
                    
                    if (Verse.Widgets.ButtonInvisible(rectStorage))
                    {
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(RimWorld.SoundDefOf.Tick_Tiny, null);
                        
                        if (storageLocked) QuickUnloadGameComp.lockedStorage.Remove(thing.thingIDNumber);
                        else QuickUnloadGameComp.lockedStorage.Add(thing.thingIDNumber);
                    }
                    
                    currentOffset += 24f; 
                }

                // --- КНОПКА 2: Замок использования ---
                if (QuickUnloadMod.settings.showConsumeLock)
                {
                    UnityEngine.Rect rectConsume = new UnityEngine.Rect(originalWidth - currentOffset, rowY + 2f, 24f, 24f);
                    bool consumeLocked = QuickUnloadGameComp.lockedConsume.Contains(thing.thingIDNumber);
                    
                    Verse.TooltipHandler.TipRegion(rectConsume, "IM.ConsumeLockTooltip".Translate());
                    
                    UnityEngine.Color baseColor = consumeLocked ? UnityEngine.Color.yellow : UnityEngine.Color.white;
                    UnityEngine.Color hoverColor = consumeLocked ? new UnityEngine.Color(1f, 1f, 0.5f) : Verse.GenUI.MouseoverColor;
                    
                    UnityEngine.GUI.color = Verse.Mouse.IsOver(rectConsume) ? hoverColor : baseColor;
                    UnityEngine.GUI.DrawTexture(rectConsume, QU_Textures.IconConsume);
                    UnityEngine.GUI.color = UnityEngine.Color.white;
                    
                    if (Verse.Widgets.ButtonInvisible(rectConsume))
                    {
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(RimWorld.SoundDefOf.Tick_Tiny, null);
                        
                        if (consumeLocked) QuickUnloadGameComp.lockedConsume.Remove(thing.thingIDNumber);
                        else QuickUnloadGameComp.lockedConsume.Add(thing.thingIDNumber);
                    }
                }
            }
        }
    }
    // --- 3. Блокируем ВАНИЛЬНУЮ авто-выгрузку на склад ---
    [HarmonyPatch(typeof(Pawn_InventoryTracker), "get_FirstUnloadableThing")]
    public static class Patch_FirstUnloadableThing
    {
        // Поле "inventoryUnloadable" в ThingDef является приватным, поэтому используем FieldRef для быстрого доступа.
        private static readonly AccessTools.FieldRef<ThingDef, bool> inventoryUnloadableRef = 
            AccessTools.FieldRefAccess<ThingDef, bool>("inventoryUnloadable");

        public static void Postfix(Pawn_InventoryTracker __instance, ref ThingCount __result)
        {
            // Если ваниль ничего не нашла — выходим
            if (__result.Thing == null) return;

            // Если вещь заблокирована — ищем следующую свободную в инвентаре
            if (QuickUnloadGameComp.lockedStorage.Contains(__result.Thing.thingIDNumber))
            {
                __result = default(ThingCount); // Сбрасываем текущий результат
                
                var innerList = __instance.innerContainer;
                for (int i = 0; i < innerList.Count; i++)
                {
                    Thing thing = innerList[i];
                    // Используем кешированную ссылку на приватное поле
                    if (inventoryUnloadableRef(thing.def) && !QuickUnloadGameComp.lockedStorage.Contains(thing.thingIDNumber))
                    {
                        __result = new ThingCount(thing, thing.stackCount);
                        break;
                    }
                }
            }
        }
    }

    // --- 4. Блокируем ванильный ручной сброс (если запрещено в настройках) ---
[HarmonyPatch]
    public static class Patch_ThingOwner_TryDrop
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return HarmonyLib.AccessTools.Method(typeof(Verse.ThingOwner), "TryDrop", new System.Type[] 
            { 
                typeof(Verse.Thing), 
                typeof(Verse.IntVec3), 
                typeof(Verse.Map), 
                typeof(Verse.ThingPlaceMode), 
                typeof(int), 
                typeof(Verse.Thing).MakeByRefType(),
                typeof(System.Action<Verse.Thing, int>), 
                typeof(System.Predicate<Verse.IntVec3>) 
            });
        }

        public static bool Prefix(Verse.ThingOwner __instance, Verse.Thing thing)
        {
            // Pawn_InventoryTracker тоже находится в Verse
            if (__instance.Owner is Verse.Pawn_InventoryTracker && !QuickUnloadMod.settings.allowManualDrop && QuickUnloadGameComp.lockedStorage.Contains(thing.thingIDNumber))
            {
                Verse.Messages.Message("IM.ItemIsLocked".Translate(), RimWorld.MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }
    }

    // --- 5. Блокируем ВАНИЛЬНОЕ поедание и наркотики ---
    [HarmonyPatch(typeof(ForbidUtility), "IsForbidden", new System.Type[] { typeof(Thing), typeof(Pawn) })]
    public static class Patch_IsForbidden
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            // Если вещь в инвентаре и заблокирована от поедания - делаем её "запрещенной" для авто-поиска
            if (!__result && t != null && t.ParentHolder is Pawn_InventoryTracker && QuickUnloadGameComp.lockedConsume.Contains(t.thingIDNumber))
            {
                __result = true;
            }
        }
    }	
// --- Блокируем ИИ от авто-поедания заблокированной еды из инвентаря ---
    [HarmonyLib.HarmonyPatch(typeof(RimWorld.FoodUtility), "BestFoodInInventory")]
    public static class Patch_BestFoodInInventory
    {
        public static void Postfix(ref Verse.Thing __result)
        {
            // Если ИИ нашел еду в кармане, но на ней висит наш замочек использования:
            if (__result != null && QuickUnloadGameComp.lockedConsume.Contains(__result.thingIDNumber))
            {
                __result = null; // Обманываем ИИ, заставляя его думать, что еды нет
            }
        }
    }

// --- Блокируем ИИ от авто-приема заблокированных наркотиков из инвентаря ---
    [HarmonyLib.HarmonyPatch(typeof(RimWorld.JobGiver_TakeDrugsForDrugPolicy), "TryGiveJob")]
    public static class Patch_JobGiver_TakeDrugs
    {
        public static void Postfix(ref Verse.AI.Job __result)
        {
            // Если ИИ сгенерировал работу "Употребить" для предмета из кармана:
            if (__result != null && __result.def == RimWorld.JobDefOf.Ingest && __result.targetA.HasThing)
            {
                if (QuickUnloadGameComp.lockedConsume.Contains(__result.targetA.Thing.thingIDNumber))
                {
                    __result = null; // Отменяем эту работу
                }
            }
        }
    }

[Verse.StaticConstructorOnStartup]
    public static class QU_Textures
    {
        // Игра сама будет искать файлы с расширением .png по этим путям внутри папки Textures
        public static readonly UnityEngine.Texture2D IconStorage = Verse.ContentFinder<UnityEngine.Texture2D>.Get("UI/IconStorage");
        public static readonly UnityEngine.Texture2D IconConsume = Verse.ContentFinder<UnityEngine.Texture2D>.Get("UI/IconConsume");
		public static readonly UnityEngine.Texture2D IconUnload = Verse.ContentFinder<UnityEngine.Texture2D>.Get("UI/IconUnload");
    }	

}	