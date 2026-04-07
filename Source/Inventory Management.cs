using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace InventoryManagement
{

[HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
    public static class Patch_FloatMenuMakerMap_GetOptions
    {
        public static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
        {
            if (selectedPawns == null || selectedPawns.Count != 1) return;
            Pawn pawn = selectedPawns[0];

            IntVec3 c = IntVec3.FromVector3(clickPos);
            Map map = pawn.Map;
            if (map == null || !c.InBounds(map)) return;

// --- УЛУЧШЕННАЯ ЛОГИКА ПОИСКА ЦЕЛИ ---
            // Ищем пешку: используем 100% ванильный алгоритм таргетинга (GenUI.TargetsAt). Это гарантирует совпадение с "Арестовать/Спасти".
            TargetingParameters tp = new TargetingParameters { canTargetPawns = true, canTargetBuildings = false, canTargetItems = false };
            Pawn targetPawn = GenUI.TargetsAt(clickPos, tp, true)
                .Select(t => t.Thing as Pawn)
                .FirstOrDefault(p => p != null && p != pawn);

            if (targetPawn != null && targetPawn.inventory != null && !targetPawn.HostileTo(pawn) && (!targetPawn.RaceProps.Animal || (QuickUnloadMod.settings.allowGiveToAnimals && targetPawn.Faction != null)) && pawn.CanReach(targetPawn, PathEndMode.Touch, Danger.Deadly))
            {
if (pawn.inventory != null && pawn.inventory.innerContainer.Count > 0)
            {
                __result.Add(new FloatMenuOption("IM.GiveToPawn".Translate(targetPawn.LabelShort), delegate
                {
                    List<FloatMenuOption> subMenu = new List<FloatMenuOption>();
                    
                    // Кнопка: Передать вообще всё
                    subMenu.Add(new FloatMenuOption("IM.GiveAll".Translate(), delegate
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickGiveInventory"), targetPawn);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                    }) { iconThing = targetPawn });

                    // Группировка предметов
                    var groups = pawn.inventory.innerContainer
                        .Where(t => !(!QuickUnloadMod.settings.allowManualDrop && QuickUnloadGameComp.lockedStorage.Contains(t.thingIDNumber)))
                        .GroupBy(t => t, new ThingStackComparer());

                    foreach (var group in groups)
                    {
                        var list = group.ToList();
                        Thing first = list[0];

                        if (list.Count == 1)
                        {
subMenu.Add(new FloatMenuOption("IM.GiveItem".Translate(first.LabelCap), delegate {
                                System.Action<int> action = count => {
                                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickGiveInventory"), targetPawn, first);
                                    job.count = count;
                                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                };
                                if (QuickUnloadMod.settings.useSliderForStacks && first.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.GiveSlider".Translate() + x, 1, first.stackCount, action, first.stackCount));
                                else action(first.stackCount);
                            }) { iconThing = first });
                        }
                        else
                        {
                            subMenu.Add(new FloatMenuOption("IM.GiveItemMany".Translate(first.def.LabelCap), delegate {
                                List<FloatMenuOption> subSub = new List<FloatMenuOption>();
subSub.Add(new FloatMenuOption("IM.GiveAllItem".Translate(first.def.LabelCap), delegate {
    for (int i = 0; i < list.Count; i++) {
        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickGiveInventory"), targetPawn, list[i]);
        if (i == 0) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
        else pawn.jobs.jobQueue.EnqueueLast(job);
    }
}) { iconThing = first });

                                foreach (var item in list) {
                                    Thing local = item;
subSub.Add(new FloatMenuOption("IM.GiveItem".Translate(local.LabelCap), delegate {
                                        System.Action<int> action = count => {
                                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickGiveInventory"), targetPawn, local);
                                            job.count = count;
                                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                        };
                                        if (QuickUnloadMod.settings.useSliderForStacks && local.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.GiveSlider".Translate() + x, 1, local.stackCount, action, local.stackCount));
                                        else action(local.stackCount);
                                    }) { iconThing = local });
                                }
                                Find.WindowStack.Add(new FloatMenu(subSub));
                            }) { iconThing = first });
                        }
                    }
                    Find.WindowStack.Add(new FloatMenu(subMenu));
                }) { iconThing = targetPawn });
            }
            }
			// --- ЛОГИКА: Забрать у другой пешки ---
            // Проверка: не враг и не гость (у пленных и своих забирать можно)
            bool canTakeFrom = targetPawn != null && (targetPawn.Faction == Faction.OfPlayer || targetPawn.IsPrisonerOfColony || targetPawn.IsSlaveOfColony);
            
            if (canTakeFrom && targetPawn.inventory != null && targetPawn.inventory.innerContainer.Count > 0)
            {
                __result.Add(new FloatMenuOption("IM.TakeFromPawn".Translate(targetPawn.LabelShort), delegate
                {
                    List<FloatMenuOption> takeMenu = new List<FloatMenuOption>();
                    
                    takeMenu.Add(new FloatMenuOption("IM.TakeAll".Translate(), delegate
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickTakeInventory"), targetPawn);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                    }) { iconThing = targetPawn });

                    var groups = targetPawn.inventory.innerContainer.GroupBy(t => t, new ThingStackComparer());
                    foreach (var group in groups)
                    {
                        var list = group.ToList();
                        Thing first = list[0];

                        if (list.Count == 1)
                        {
                            takeMenu.Add(new FloatMenuOption("IM.TakeItem".Translate(first.LabelCap), delegate {
                                System.Action<int> action = count => {
                                    if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) {
                                        Verse.Messages.Message("IM.TakeFailedOverweight".Translate(first.Label, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                                        return;
                                    }
                                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickTakeInventory"), targetPawn, first);
                                    job.count = count;
                                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                };
                                if (QuickUnloadMod.settings.useSliderForStacks && first.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.TakeSlider".Translate() + x, 1, first.stackCount, action, first.stackCount));
                                else action(first.stackCount);
                            }) { iconThing = first });
                        }
                        else
                        {
                            takeMenu.Add(new FloatMenuOption("IM.TakeItemMany".Translate(first.def.LabelCap), delegate {
                                List<FloatMenuOption> subSub = new List<FloatMenuOption>();
                                subSub.Add(new FloatMenuOption("IM.TakeAllItem".Translate(first.def.LabelCap), delegate {
                                    bool msgShown = false;
                                    float currentMass = MassUtility.GearAndInventoryMass(pawn);
                                    float capacity = MassUtility.Capacity(pawn);
                                    for (int i = 0; i < list.Count; i++) {
                                        Thing item = list[i];
                                        if (currentMass >= capacity) {
                                            if (!msgShown) { Verse.Messages.Message("IM.TakeAllPartialOverweight".Translate(first.def.LabelCap, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false); msgShown = true; }
                                            break;
                                        }
                                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickTakeInventory"), targetPawn, item);
                                        job.count = item.stackCount;
                                        if (i == 0) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                        else pawn.jobs.jobQueue.EnqueueLast(job);
                                        currentMass += item.stackCount * item.GetStatValue(StatDefOf.Mass);
                                    }
                                }) { iconThing = first });

                                foreach (var item in list) {
                                    Thing local = item;
                                    subSub.Add(new FloatMenuOption("IM.TakeItem".Translate(local.LabelCap), delegate {
                                        System.Action<int> action = count => {
                                            if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) {
                                                Verse.Messages.Message("IM.TakeFailedOverweight".Translate(local.Label, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                                                return;
                                            }
                                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickTakeInventory"), targetPawn, local);
                                            job.count = count;
                                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                        };
                                        if (QuickUnloadMod.settings.useSliderForStacks && local.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.TakeSlider".Translate() + x, 1, local.stackCount, action, local.stackCount));
                                        else action(local.stackCount);
                                    }) { iconThing = local });
                                }
                                Find.WindowStack.Add(new FloatMenu(subSub));
                            }) { iconThing = first });
                        }
                    }
                    Find.WindowStack.Add(new FloatMenu(takeMenu));
                }) { iconThing = targetPawn });
            }
			

            // --- ЛОГИКА ДЛЯ СКЛАДА ---
            SlotGroup slotGroup = c.GetSlotGroup(map);
            if (slotGroup != null && pawn.CanReach(c, PathEndMode.ClosestTouch, Danger.Deadly))
            {
if (pawn.inventory != null && pawn.inventory.innerContainer.Count > 0)
            {
                __result.Add(new FloatMenuOption("IM.Store".Translate(), delegate
                {
                    List<FloatMenuOption> subMenu = new List<FloatMenuOption>();
                    
                    // Кнопка: Сложить вообще всё (кроме заблокированного)
                    subMenu.Add(new FloatMenuOption("IM.StoreInventory".Translate(), delegate
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnloadInventory"), c);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                    }));

                    // Группировка предметов
                    var groups = pawn.inventory.innerContainer
                        .Where(t => !(!QuickUnloadMod.settings.allowManualDrop && QuickUnloadGameComp.lockedStorage.Contains(t.thingIDNumber)))
                        .GroupBy(t => t, new ThingStackComparer());

                    foreach (var group in groups)
                    {
                        var list = group.ToList();
                        Thing first = list[0];

                        if (list.Count == 1)
                        {
subMenu.Add(new FloatMenuOption("IM.StoreItem".Translate(first.LabelCap), delegate {
                                System.Action<int> action = count => {
                                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnloadInventory"), c, first);
                                    job.count = count;
                                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                };
                                if (QuickUnloadMod.settings.useSliderForStacks && first.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.StoreSlider".Translate() + x, 1, first.stackCount, action, first.stackCount));
                                else action(first.stackCount);
                            }) { iconThing = first });
                        }
                        else
                        {
                            subMenu.Add(new FloatMenuOption("IM.StoreItemMany".Translate(first.def.LabelCap), delegate {
                                List<FloatMenuOption> subSub = new List<FloatMenuOption>();
subSub.Add(new FloatMenuOption("IM.StoreAllItem".Translate(first.def.LabelCap), delegate {
    for (int i = 0; i < list.Count; i++) {
        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnloadInventory"), c, list[i]);
        if (i == 0) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
        else pawn.jobs.jobQueue.EnqueueLast(job);
    }
}) { iconThing = first });

                                foreach (var item in list) {
                                    Thing local = item;
subSub.Add(new FloatMenuOption("IM.StoreItem".Translate(local.LabelCap), delegate {
                                        System.Action<int> action = count => {
                                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnloadInventory"), c, local);
                                            job.count = count;
                                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                        };
                                        if (QuickUnloadMod.settings.useSliderForStacks && local.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.StoreSlider".Translate() + x, 1, local.stackCount, action, local.stackCount));
                                        else action(local.stackCount);
                                    }) { iconThing = local });
                                }
                                Find.WindowStack.Add(new FloatMenu(subSub));
                            }) { iconThing = first });
                        }
                    }
                    Find.WindowStack.Add(new FloatMenu(subMenu));
                }));
            }

                // Оптимизация: проверяем наличие предметов без лишних аллокаций
                bool hasTakeableItems = false;
                foreach (var t in slotGroup.HeldThings) { if (!(t is Corpse)) { hasTakeableItems = true; break; } }
                
                if (hasTakeableItems)
                {
                    __result.Add(new FloatMenuOption("IM.Take".Translate(), delegate
                    {
                        List<FloatMenuOption> subMenu = new List<FloatMenuOption>();
                        subMenu.Add(new FloatMenuOption("IM.TakeAllFromStorage".Translate(), delegate
                        {
                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickLoadInventory"), c);
                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                        }));

                        // --- ГРУППИРОВКА ПРЕДМЕТОВ НА СКЛАДЕ ---
                        var groupedItems = slotGroup.HeldThings
                            .Where(t => !(t is Corpse))
                            .GroupBy(t => t, new ThingStackComparer());

                        foreach (var group in groupedItems)
                        {
                            var groupList = group.ToList();
                            
                            if (groupList.Count == 1)
                            {
                                // Один стак
                                Thing localItem = groupList[0];
                                subMenu.Add(new FloatMenuOption("IM.TakeItemFromStorage".Translate(localItem.LabelCap), delegate
                                {
System.Action<int> action = count => {
                                    if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) {
                                        Verse.Messages.Message("IM.TakeFailedOverweightFromStorage".Translate(localItem.Label, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                                        return;
                                    }
                                    Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, localItem);
                                    job.count = count;
                                    job.checkEncumbrance = false; // Отключаем ванильный запрет!
                                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                };
                                if (QuickUnloadMod.settings.useSliderForStacks && localItem.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.TakeSliderFromStorage".Translate() + x, 1, localItem.stackCount, action, localItem.stackCount));
                                else action(localItem.stackCount);
                                })
                                {
                                    iconThing = localItem
                                });
                            }
                           else
{
    // Несколько стаков (подменю)
    Thing firstItem = groupList[0];
    string groupLabel = firstItem.def.LabelCap;

    subMenu.Add(new FloatMenuOption("IM.TakeItemManyFromStorage".Translate(groupLabel), delegate
    {
        List<FloatMenuOption> subSubMenu = new List<FloatMenuOption>();

        // Кнопка "Взять всё" (исправленная, с очередью задач)
subSubMenu.Add(new FloatMenuOption("IM.TakeAllItemFromStorage".Translate(groupLabel), delegate
        {
            bool msgShown = false;
            float currentMass = MassUtility.GearAndInventoryMass(pawn);
            float capacity = MassUtility.Capacity(pawn);

            for (int i = 0; i < groupList.Count; i++)
            {
                Thing item = groupList[i];
                if (currentMass >= capacity) {
                    if (!msgShown) { Verse.Messages.Message("IM.TakeAllPartialOverweightFromStorage".Translate(groupLabel, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false); msgShown = true; }
                    break;
                }

                Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, item);
                job.count = item.stackCount;
                job.checkEncumbrance = false;

                if (i == 0) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                else pawn.jobs.jobQueue.EnqueueLast(job);
                
                // Добавляем вес для проверки следующего стака в цикле
                currentMass += item.stackCount * item.GetStatValue(StatDefOf.Mass);
            }
        }) { iconThing = firstItem });

        // Кнопки для каждого стака по отдельности
        foreach (Thing t in groupList)
        {
            Thing localItem = t;
            subSubMenu.Add(new FloatMenuOption("IM.TakeItemFromStorage".Translate(localItem.LabelCap), delegate
            {
System.Action<int> action = count => {
                                    if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) {
                                        Verse.Messages.Message("IM.TakeFailedOverweightFromStorage".Translate(localItem.Label, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                                        return;
                                    }
                                    Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, localItem);
                                    job.count = count;
                                    job.checkEncumbrance = false; // Отключаем ванильный запрет!
                                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                };
                                if (QuickUnloadMod.settings.useSliderForStacks && localItem.stackCount > 1) Find.WindowStack.Add(new Dialog_Slider(x => "IM.TakeSliderFromStorage".Translate() + x, 1, localItem.stackCount, action, localItem.stackCount));
                                else action(localItem.stackCount);
            }) { iconThing = localItem });
        }
        Find.WindowStack.Add(new FloatMenu(subSubMenu));
    }) { iconThing = firstItem });
}
                            }
                        
                        // --- КОНЕЦ ГРУППИРОВКИ ---

                        Find.WindowStack.Add(new FloatMenu(subMenu));
                    }));
                }

                if (pawn.apparel != null)
                {
                    // Оптимизация: поиск одежды без лишних аллокаций LINQ
                    var availableApparel = new List<Apparel>();
                    foreach (var t in slotGroup.HeldThings) { if (t is Apparel ap) availableApparel.Add(ap); }
                    
                    bool hasWorn = pawn.apparel.WornApparel.Count > 0;
                    bool hasAvailable = availableApparel.Count > 0;

                    if (hasWorn || hasAvailable)
                    {
                        __result.Add(new FloatMenuOption("IM.Gear".Translate(), delegate
                        {
                            List<FloatMenuOption> equipMenu = new List<FloatMenuOption>();

                            if (hasWorn)
                            {
                                equipMenu.Add(new FloatMenuOption("IM.UnequipApparel".Translate(), delegate
                                {
                                    List<FloatMenuOption> unequipMenu = new List<FloatMenuOption>();
                                    unequipMenu.Add(new FloatMenuOption("IM.UnequipAll".Translate(), delegate
                                    {
                                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnequipApparel"), c);
                                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                    }));
                                    foreach (Apparel ap in pawn.apparel.WornApparel)
                                    {
                                        Apparel localAp = ap;
                                        unequipMenu.Add(new FloatMenuOption("IM.UnequipItem".Translate(localAp.LabelCap), delegate
                                        {
                                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickUnequipApparel"), c, localAp);
                                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                        })
                                        { iconThing = localAp });
                                    }
                                    Find.WindowStack.Add(new FloatMenu(unequipMenu));
                                }));
                            }

                            if (hasAvailable)
                            {
                                equipMenu.Add(new FloatMenuOption("IM.EquipApparel".Translate(), delegate
                                {
                                    List<FloatMenuOption> wearMenu = new List<FloatMenuOption>();
                                    wearMenu.Add(new FloatMenuOption("IM.WearAll".Translate(), delegate
                                    {
                                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickEquipApparel"), c);
                                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                    }));
                                    foreach (Apparel ap in availableApparel)
                                    {
                                        Apparel localAp = ap;
                                        wearMenu.Add(new FloatMenuOption("IM.WearItem".Translate(localAp.LabelCap), delegate
                                        {
                                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("QuickEquipApparel"), c, localAp);
                                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                                        })
                                        { iconThing = localAp });
                                    }
                                    Find.WindowStack.Add(new FloatMenu(wearMenu));
                                }));
                            }

                            Find.WindowStack.Add(new FloatMenu(equipMenu));
                        }));
                    }
                }
            }
            // --- ЛОГИКА ДЛЯ ПРЕДМЕТОВ НА ЗЕМЛЕ ---
            // Ищем предметы: ваниль при подборе и экипировке всегда ищет предметы строго в клетке клика (c.GetThingList)
            var nearbyItems = c.GetThingList(map)
                .Where(t => t.def.category == ThingCategory.Item && !(t is Corpse) && pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Deadly));

            // Исключаем те, что уже показаны в меню склада, чтобы не было дубликатов
            if (slotGroup != null) nearbyItems = nearbyItems.Where(t => !slotGroup.HeldThings.Contains(t));

            foreach (Thing t in nearbyItems)
            {
                Thing localItem = t;
                __result.Add(new FloatMenuOption("IM.TakeItemFromStorage".Translate(localItem.LabelCap), delegate
                {
                    System.Action<int> action = count => {
                        if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) {
                            Verse.Messages.Message("IM.TakeFailedOverweightFromStorage".Translate(localItem.Label, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, localItem);
                        job.count = count;
                        job.checkEncumbrance = false;
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, KeyBindingDefOf.QueueOrder.IsDown);
                    };
                    if (QuickUnloadMod.settings.useSliderForStacks && localItem.stackCount > 1) 
                        Find.WindowStack.Add(new Dialog_Slider(x => "IM.TakeSliderFromStorage".Translate() + x, 1, localItem.stackCount, action, localItem.stackCount));
                    else action(localItem.stackCount);
                })
                {
                    iconThing = localItem
                });
            }
            }
        }
    

// Драйвер 1: Подойти и сбросить вещи (с учетом галочки в настройках)
    public class JobDriver_QuickUnloadInventory : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);

            Toil drop = ToilMaker.MakeToil("DropInventory");
            drop.initAction = delegate
            {
                SlotGroup slotGroup = job.targetA.Cell.GetSlotGroup(pawn.Map);
                if (slotGroup == null) return;

                void TryStore(Thing item)
                {
                    // УМНОЕ АВТО-РАЗРЕШЕНИЕ: Пробуем разрешить и проверяем, приняла ли игра этот предмет
                    if (QuickUnloadMod.settings.ignoreStorageSettings)
                    {
                        slotGroup.Settings.filter.SetAllow(item.def, true);
                        if (!slotGroup.Settings.AllowedToAccept(item)) return; // Если всё равно false - значит физически нельзя
                    }
                    else if (!slotGroup.Settings.AllowedToAccept(item))
                    {
                        return;
                    }

                    foreach (IntVec3 c in slotGroup.CellsList)
                    {
                        // Пока предмет не кончился, пытаемся забить текущую клетку (важно для сундуков/ящиков с большой вместимостью)
                        while (item != null && !item.Destroyed && item.stackCount > 0 && pawn.inventory.innerContainer.Contains(item))
                        {
                            int itemsInCell = 0; int maxItems = 1;
                            Thing mergeable = null;

                            foreach (Thing t in pawn.Map.thingGrid.ThingsListAt(c)) {
                                if (t.def.category == ThingCategory.Item) {
                                    itemsInCell++;
                                    // Ищем ЛЮБОЙ неполный стак такого же типа в этой клетке
                                    if (mergeable == null && t.def == item.def && t.stackCount < t.def.stackLimit) mergeable = t;
                                }
                                if (t.def.building != null && t.def.building.maxItemsInCell > maxItems) maxItems = t.def.building.maxItemsInCell;
                            }

                            if (mergeable != null)
                            {
                                int countToDrop = Mathf.Min(item.stackCount, mergeable.def.stackLimit - mergeable.stackCount);
                                if (!pawn.inventory.innerContainer.TryDrop(item, c, pawn.Map, ThingPlaceMode.Direct, countToDrop, out _)) break;
                            }
                            else if (itemsInCell < maxItems)
                            {
                                int countToDrop = Mathf.Min(item.stackCount, item.def.stackLimit);
                                if (!pawn.inventory.innerContainer.TryDrop(item, c, pawn.Map, ThingPlaceMode.Direct, countToDrop, out Thing dropped)) break;
                                if (dropped != null) dropped.Rotation = Rot4.North;
                            }
                            else
                            {
                                break; // Клетка полностью забита (все слоты заняты)
                            }
                        }
                        if (item == null || item.Destroyed || item.stackCount <= 0) return;
                    }
                }

if (job.targetB != null && job.targetB.HasThing)
                {
                    Thing item = job.targetB.Thing;
                    if (QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber) && !QuickUnloadMod.settings.allowManualDrop) return;
                    if (pawn.inventory.innerContainer.Contains(item)) TryStore(item);
                }
                else
                {
                    foreach (Thing item in pawn.inventory.innerContainer.ToList())
                    {
                        // Сложить всё: ПРОПУСКАЕМ вещи с замочками
                        if (pawn.inventory.innerContainer.Contains(item) && !QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber)) TryStore(item);
                    }
                }
            };
            yield return drop;
        }
    }

    // Драйвер 2: Подойти и забрать ВСЕ вещи со склада до перевеса
    public class JobDriver_QuickLoadInventory : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);

            Toil take = ToilMaker.MakeToil("TakeAllInventory");
take.initAction = delegate
            {
                SlotGroup group = TargetA.Cell.GetSlotGroup(pawn.Map);
                if (group == null) return;

                bool msgShown = false;
                foreach (Thing t in group.HeldThings.ToList())
                {
                    if (t is Corpse) continue;

                    // Если УЖЕ есть перегруз - останавливаемся
                    if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) 
                    {
                        if (!msgShown) { Verse.Messages.Message("IM.TakeAllPartialOverweightGeneral".Translate(pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false); msgShown = true; }
                        break;
                    }

                    pawn.inventory.innerContainer.TryAddOrTransfer(t.SplitOff(t.stackCount));
                }
            };
            yield return take;
        }
    }

// НОВЫЙ ДРАЙВЕР: Подойти к пешке и передать вещи
    public class JobDriver_QuickGiveInventory : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Идем к целевой пешке (используем GotoThing, чтобы следовать за ней, если она движется)
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil give = ToilMaker.MakeToil("GiveInventory");
            give.initAction = delegate
            {
                Pawn targetPawn = job.targetA.Thing as Pawn;
                if (targetPawn == null || targetPawn.inventory == null) return;

void TryGive(Thing item)
                {
                    // Если у получателя УЖЕ есть перегруз - запрещаем
                    if (MassUtility.GearAndInventoryMass(targetPawn) >= MassUtility.Capacity(targetPawn)) 
                    {
                        Verse.Messages.Message("IM.GiveFailedOverweight".Translate(item.Label, targetPawn.LabelShort), targetPawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                        return; 
                    }

                    int requestedCount = (job.count > 0) ? job.count : item.stackCount;
                    pawn.inventory.innerContainer.TryTransferToContainer(item, targetPawn.inventory.innerContainer, requestedCount, out _);
                }

if (job.targetB != null && job.targetB.HasThing)
                {
                    Thing item = job.targetB.Thing;
                    if (QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber) && !QuickUnloadMod.settings.allowManualDrop) return;
                    if (pawn.inventory.innerContainer.Contains(item)) TryGive(item);
                }
                else
                {
                    foreach (Thing item in pawn.inventory.innerContainer.ToList())
                    {
                        // Передать всё: ПРОПУСКАЕМ вещи с замочками
                        if (pawn.inventory.innerContainer.Contains(item) && !QuickUnloadGameComp.lockedStorage.Contains(item.thingIDNumber)) TryGive(item);
                    }
                }
            };
            yield return give;
        }
    }	

// ДРАЙВЕР: Снять одежду и положить на склад
    public class JobDriver_QuickUnequipApparel : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);

            Toil drop = ToilMaker.MakeToil("QuickUnequipApparel");
            drop.initAction = delegate
            {
                SlotGroup slotGroup = job.targetA.Cell.GetSlotGroup(pawn.Map);
                if (slotGroup == null) return;

                void TryStoreApparel(Apparel wornApparel)
                {
                    // УМНОЕ АВТО-РАЗРЕШЕНИЕ для одежды
                    if (QuickUnloadMod.settings.ignoreStorageSettings)
                    {
                        slotGroup.Settings.filter.SetAllow(wornApparel.def, true);
                        if (!slotGroup.Settings.AllowedToAccept(wornApparel)) return;
                    }
                    else if (!slotGroup.Settings.AllowedToAccept(wornApparel))
                    {
                        return;
                    }

                    foreach (IntVec3 c in slotGroup.CellsList)
                    {
                        if (wornApparel == null || wornApparel.Destroyed || !pawn.apparel.WornApparel.Contains(wornApparel)) return;

                        int itemsInCell = 0; int maxItems = 1; 
                        foreach (Thing t in pawn.Map.thingGrid.ThingsListAt(c))
                        {
                            if (t.def.category == ThingCategory.Item) itemsInCell++;
                            if (t.def.building != null && t.def.building.maxItemsInCell > maxItems) maxItems = t.def.building.maxItemsInCell;
                        }
                        
                        if (itemsInCell < maxItems)
                        {
                            pawn.apparel.Remove(wornApparel);
                            if (GenPlace.TryPlaceThing(wornApparel, c, pawn.Map, ThingPlaceMode.Direct, out Thing dropped))
                            {
                                if (dropped != null) dropped.Rotation = Rot4.North;
                            }
                            return; 
                        }
                    }
                }

                if (job.targetB != null && job.targetB.HasThing)
                {
                    Apparel ap = job.targetB.Thing as Apparel;
                    if (pawn.apparel.WornApparel.Contains(ap)) TryStoreApparel(ap);
                }
                else
                {
                    foreach (Apparel ap in pawn.apparel.WornApparel.ToList())
                    {
                        TryStoreApparel(ap);
                    }
                }
            };
            yield return drop;
        }
    }

    // ДРАЙВЕР: Надеть одежду со склада
    public class JobDriver_QuickEquipApparel : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);

            Toil equip = ToilMaker.MakeToil("QuickEquipApparel");
            equip.initAction = delegate
            {
                SlotGroup group = job.targetA.Cell.GetSlotGroup(pawn.Map);
                if (group == null) return;

                void TryEquip(Apparel ap)
                {
                    bool canWear = true;
                    // Проверяем, не конфликтует ли эта вещь с уже надетой (по слоям и частям тела)
                    foreach (Apparel worn in pawn.apparel.WornApparel)
                    {
                        if (!ApparelUtility.CanWearTogether(ap.def, worn.def, pawn.RaceProps.body))
                        {
                            canWear = false;
                            break;
                        }
                    }
                    
                    if (canWear)
                    {
                        if (ap.Spawned) ap.DeSpawn();
                        pawn.apparel.Wear(ap, false);
                    }
                }

                if (job.targetB != null && job.targetB.HasThing)
                {
                    Apparel ap = job.targetB.Thing as Apparel;
                    if (ap != null && group.HeldThings.Contains(ap)) TryEquip(ap);
                }
                else
                {
                    // Пытаемся надеть все вещи со склада по списку
                    foreach (Thing t in group.HeldThings.ToList())
                    {
                        if (t is Apparel ap) TryEquip(ap);
                    }
                }
            };
            yield return equip;
        }
    }	

// --- Очистка мертвых ID при уничтожении предмета (чтобы не пухло сохранение) ---
    [HarmonyPatch(typeof(Verse.Thing), "Destroy")]
    public static class Patch_Thing_Destroy
    {
        public static void Postfix(Verse.Thing __instance)
        {
            // БЫСТРЫЙ ВЫХОД: Если заблокированных вещей нет, ничего не делаем
            if (QuickUnloadGameComp.lockedStorage.Count == 0 && QuickUnloadGameComp.lockedConsume.Count == 0) return;

            if (__instance.def?.category == Verse.ThingCategory.Item)
            {
                QuickUnloadGameComp.lockedStorage.Remove(__instance.thingIDNumber);
                QuickUnloadGameComp.lockedConsume.Remove(__instance.thingIDNumber);
            }
        }
    }

// ДРАЙВЕР: Подойти к пешке и забрать вещи в свой инвентарь
    public class JobDriver_QuickTakeInventory : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed) && (job.targetB == null || !job.targetB.HasThing || pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed));

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil take = ToilMaker.MakeToil("TakeInventoryFromPawn");
            take.initAction = delegate
            {
                Pawn targetPawn = job.targetA.Thing as Pawn;
                if (targetPawn == null || targetPawn.inventory == null) return;

                void TryTake(Thing item)
                {
                    if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) 
                    {
                        Verse.Messages.Message("IM.TakeFailedOverweight".Translate(item.Label, pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                        return; 
                    }
                    int requestedCount = (job.count > 0) ? job.count : item.stackCount;
                    // Переносим ИЗ цели В активную пешку
                    targetPawn.inventory.innerContainer.TryTransferToContainer(item, pawn.inventory.innerContainer, requestedCount, out _);
                }

                if (job.targetB != null && job.targetB.HasThing)
                {
                    Thing item = job.targetB.Thing;
                    if (targetPawn.inventory.innerContainer.Contains(item)) TryTake(item);
                }
                else
                {
                    bool msgShown = false;
                    foreach (Thing item in targetPawn.inventory.innerContainer.ToList())
                    {
                        if (MassUtility.GearAndInventoryMass(pawn) >= MassUtility.Capacity(pawn)) 
                        {
                            if (!msgShown) { Verse.Messages.Message("IM.TakeAllPartialOverweightGeneral".Translate(pawn.LabelShort), pawn, RimWorld.MessageTypeDefOf.RejectInput, false); msgShown = true; }
                            break;
                        }
                        targetPawn.inventory.innerContainer.TryTransferToContainer(item, pawn.inventory.innerContainer, item.stackCount, out _);
                    }
                }
            };
            yield return take;
        }
    }	
// Универсальный класс, который учит LINQ (GroupBy) правильно сравнивать предметы
public class ThingStackComparer : IEqualityComparer<Thing>
{
    public bool Equals(Thing x, Thing y)
    {
        if (x == y) return true;
        if (x == null || y == null) return false;
        
        // Магия здесь: игра сама проверит твой яд, качество, материал (Stuff) и любые другие моды
        return x.CanStackWith(y);
    }

    public int GetHashCode(Thing obj)
    {
        if (obj == null) return 0;
        int hash = obj.def.GetHashCode();
        if (obj.Stuff != null) hash ^= obj.Stuff.GetHashCode();
        // Добавляем качество для уменьшения коллизий
        if (obj.TryGetQuality(out QualityCategory qc)) hash ^= (int)qc * 397;
        return hash;
    }
}	
	
}