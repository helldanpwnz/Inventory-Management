using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace InventoryManagement
{

    public class QuickUnloadSettings : Verse.ModSettings
    {
        public bool ignoreStorageSettings = false;
        public bool allowManualDrop = true;
        // ДВЕ НОВЫЕ НАСТРОЙКИ (по умолчанию включены)
        public bool showStorageLock = true; 
        public bool showConsumeLock = true; 
		public bool showUnloadGizmo = false;
		public bool useSliderForStacks = true;
		public bool enableDropCountSlider = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Verse.Scribe_Values.Look(ref ignoreStorageSettings, "ignoreStorageSettings", false);
            Verse.Scribe_Values.Look(ref allowManualDrop, "allowManualDrop", true);
            Verse.Scribe_Values.Look(ref showStorageLock, "showStorageLock", true);
            Verse.Scribe_Values.Look(ref showConsumeLock, "showConsumeLock", true);
			Scribe_Values.Look(ref useSliderForStacks, "useSliderForStacks", true);
			Scribe_Values.Look(ref showUnloadGizmo, "showUnloadGizmo", false);
			Scribe_Values.Look(ref enableDropCountSlider, "enableDropCountSlider", true);
        }
    }

    public class QuickUnloadMod : Verse.Mod
    {
        public static QuickUnloadSettings settings;

        public QuickUnloadMod(Verse.ModContentPack content) : base(content)
        {
            settings = GetSettings<QuickUnloadSettings>();
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Verse.Listing_Standard listing = new Verse.Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("IM.IgnoreStorageSettings".Translate(), ref settings.ignoreStorageSettings, "IM.IgnoreStorageSettingsDesc".Translate());
            listing.CheckboxLabeled("IM.AllowManualDrop".Translate(), ref settings.allowManualDrop);
            
            listing.Gap();
            listing.CheckboxLabeled("IM.ShowStorageLock".Translate(), ref settings.showStorageLock);
            listing.CheckboxLabeled("IM.ShowConsumeLock".Translate(), ref settings.showConsumeLock);
			listing.CheckboxLabeled("IM.ShowUnloadGizmo".Translate(), ref settings.showUnloadGizmo);
			listing.CheckboxLabeled("IM.UseSliderForStacks".Translate(), ref settings.useSliderForStacks);
			listing.CheckboxLabeled("IM.EnableDropCountSlider".Translate(), ref settings.enableDropCountSlider);
            
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "Inventory Management";
    }
}