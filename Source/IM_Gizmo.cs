using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace InventoryManagement
{
[HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            // Сначала отдаем все стандартные кнопки
            foreach (var g in __result) yield return g;

            // 1. Базовые проверки: настройки и контроль игрока
            if (!QuickUnloadMod.settings.showUnloadGizmo || !__instance.IsColonistPlayerControlled) yield break;

            // 2. Проверяем, есть ли ВООБЩЕ что-то в инвентаре, кроме заблокированного
            bool hasItems = false;
            if (__instance.inventory?.innerContainer != null)
            {
                foreach (var item in __instance.inventory.innerContainer)
                {
                    if (!QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber))
                    {
                        hasItems = true;
                        break;
                    }
                }
            }

            // 3. Если есть вещи — показываем кнопку ВСЕГДА
            if (hasItems)
            {
                yield return new Command_Action
                {
                    defaultLabel = "IM.Unload".Translate(),
                    defaultDesc = "IM.UnloadDesc".Translate(),
                    icon = QU_Textures.IconUnload,
                    action = delegate
                    {
                        // Снимаем призыв при нажатии (чтобы пешка сразу пошла разгружаться)
                        if (__instance.Drafted) __instance.drafter.Drafted = false;

                        HashSet<SlotGroup> targetGroups = new HashSet<SlotGroup>();
                        foreach (var item in __instance.inventory.innerContainer)
                        {
                            if (QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber)) continue;
                            if (StoreUtility.TryFindBestBetterStoreCellFor(item, __instance, __instance.Map, StoragePriority.Unstored, __instance.Faction, out IntVec3 cell))
                            {
                                SlotGroup sg = cell.GetSlotGroup(__instance.Map);
                                if (sg != null) targetGroups.Add(sg);
                            }
                        }

                        if (targetGroups.Count > 0)
                        {
                            bool first = true;
                            foreach (var group in targetGroups)
                            {
                                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnloadInventory"), group.CellsList[0]);
                                if (first) { __instance.jobs.TryTakeOrderedJob(job, JobTag.Misc); first = false; }
                                else { __instance.jobs.jobQueue.EnqueueLast(job); }
                            }
                        }
                        else
                        {
                            Messages.Message("IM.NoStorageFound".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                    }
                };
            }
        }
    }

	
	
}		