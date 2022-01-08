﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abilifier.Framework;
using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS.Collections;
using HBS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Abilifier.Patches
{
    public class EffectDataExtensionManager
    {
        public class EffectDataExtension
        {
            public TagSet TargetComponentTagMatch = new TagSet();
        }

        private static EffectDataExtensionManager _instance;

        public ConcurrentDictionary<string, EffectDataExtensionManager.EffectDataExtension> ExtendedEffectDataDict =
            new ConcurrentDictionary<string, EffectDataExtensionManager.EffectDataExtension>();

        public static EffectDataExtensionManager ManagerInstance
        {
            get
            {
                if (_instance == null) _instance = new EffectDataExtensionManager();
                return _instance;
            }
        }

        internal void Initialize()
        {
            using (StreamReader reader = new StreamReader($"{Mod.modDir}/EffectDataExtensions.json"))
            {
                string jdata = reader.ReadToEnd(); //dictionary key should match EffectData.Description.Id of whatever Effect you want to, ahem, affect.
                ExtendedEffectDataDict = JsonConvert.DeserializeObject<ConcurrentDictionary<string, EffectDataExtension>>(jdata);
                //deser separate setting thing here
                Mod.modLog.LogMessage($"Adding effectData restriction for {ExtendedEffectDataDict}!");
            }
        }
    }

    public static class EffectDataExtensions
    {
        public static EffectDataExtensionManager.EffectDataExtension getStatDataExtension(this EffectData statData)
        {
            return EffectDataExtensionManager.ManagerInstance.ExtendedEffectDataDict.ContainsKey(
                statData.Description.Id)
                ? EffectDataExtensionManager.ManagerInstance.ExtendedEffectDataDict[statData.Description.Id]
                : new EffectDataExtensionManager.EffectDataExtension();
        }

        public static List<MechComponent> GetTargetComponentsMatchingTags(ICombatant target,
            StatisticEffectData.TargetCollection targetCollection, WeaponSubType weaponSubType, WeaponType weaponType,
            WeaponCategoryValue weaponCategoryValue, AmmoCategoryValue ammoCategoryValue, TagSet tagSet)
        {
            List<MechComponent> list = new List<MechComponent>();
            if (targetCollection == StatisticEffectData.TargetCollection.SingleRandomWeapon)
            {
                AbstractActor abstractActor = target as AbstractActor;
                if (abstractActor != null)
                {
                    List<Weapon> list2 = abstractActor.Weapons.FindAll((Weapon x) =>
                        !x.IsDisabled && tagSet.Overlaps(x.componentDef.ComponentTags));
                    if (list2.Count > 0)
                    {
                        list.Add(list2.GetRandomElement());
                    }
                }
            }
            else if (targetCollection == StatisticEffectData.TargetCollection.StrongestWeapon)
            {
                AbstractActor abstractActor2 = target as AbstractActor;
                if (abstractActor2 != null)
                {
                    List<Weapon> list3 = abstractActor2.Weapons.FindAll((Weapon x) =>
                        !x.IsDisabled && tagSet.Overlaps(x.componentDef.ComponentTags));
                    if (list3.Count > 0)
                    {
                        list3.Sort((Weapon a, Weapon b) =>
                            b.DamagePerShot.CompareTo(a.DamagePerShot * (float) a.ShotsWhenFired));
                        list.Add(list3[0]);
                    }
                }
            }
            else if (targetCollection == StatisticEffectData.TargetCollection.Weapon)
            {
                AbstractActor abstractActor3 = target as AbstractActor;
                if (abstractActor3 != null)
                {
                    List<Weapon> list4 = new List<Weapon>();
                    if (weaponSubType != WeaponSubType.NotSet)
                    {
                        if (weaponSubType == WeaponSubType.Melee)
                        {
                            Mech mech = abstractActor3 as Mech;
                            if (mech != null)
                            {
                                list4.Add(mech.MeleeWeapon);
                            }
                        }
                        else if (weaponSubType == WeaponSubType.DFA)
                        {
                            Mech mech2 = abstractActor3 as Mech;
                            if (mech2 != null)
                            {
                                list4.Add(mech2.DFAWeapon);
                            }
                        }
                        else
                        {
                            list4 = abstractActor3.Weapons.FindAll((Weapon x) =>
                                x.WeaponSubType == weaponSubType && tagSet.Overlaps(x.componentDef.ComponentTags));
                        }
                    }
                    else if (weaponType != WeaponType.NotSet)
                    {
                        list4 = abstractActor3.Weapons.FindAll((Weapon x) =>
                            x.Type == weaponType && tagSet.Overlaps(x.componentDef.ComponentTags));
                    }
                    else if (!weaponCategoryValue.Is_NotSet)
                    {
                        list4 = abstractActor3.Weapons.FindAll((Weapon x) =>
                            x.WeaponCategoryValue.ID == weaponCategoryValue.ID &&
                            tagSet.Overlaps(x.componentDef.ComponentTags));
                    }
                    else
                    {
                        list4 = new List<Weapon>(
                            abstractActor3.Weapons.FindAll(x => tagSet.Overlaps(x.componentDef.ComponentTags)));
                    }

                    for (int i = 0; i < list4.Count; i++)
                    {
                        list.Add(list4[i]);
                    }
                }
            }
            else if (targetCollection == StatisticEffectData.TargetCollection.AmmoBox)
            {
                AbstractActor abstractActor4 = target as AbstractActor;
                if (abstractActor4 != null)
                {
                    List<AmmunitionBox> list5 = new List<AmmunitionBox>();
                    if (!ammoCategoryValue.Is_NotSet)
                    {
                        list5 = abstractActor4.ammoBoxes.FindAll((AmmunitionBox x) =>
                            x.ammoCategoryValue.Equals(ammoCategoryValue) &&
                            tagSet.Overlaps(x.componentDef.ComponentTags));
                    }
                    else
                    {
                        list5 = new List<AmmunitionBox>(
                            abstractActor4.ammoBoxes.FindAll(x => tagSet.Overlaps(x.componentDef.ComponentTags)));
                    }

                    for (int j = 0; j < list5.Count; j++)
                    {
                        list.Add(list5[j]);
                    }
                }
            }

            return list;
        }

        public class EffectDataExtensionPatches
        {
            [HarmonyPatch(typeof(EffectManager), "GetTargetStatCollections")]
            public static class EffectManager_GetTargetStatCollections
            {
                public static bool Prefix(EffectManager __instance, EffectData effectData, ICombatant target,
                    ref List<StatCollection> __result)
                {
                    List<StatCollection> list = new List<StatCollection>();
                    StatisticEffectData.TargetCollection targetCollection = effectData.statisticData.targetCollection;
                    WeaponSubType targetWeaponSubType = effectData.statisticData.targetWeaponSubType;
                    WeaponType targetWeaponType = effectData.statisticData.targetWeaponType;
                    WeaponCategoryValue targetWeaponCategoryValue = effectData.statisticData.TargetWeaponCategoryValue;
                    AmmoCategoryValue targetAmmoCategoryValue = effectData.statisticData.TargetAmmoCategoryValue;

                    if (effectData.getStatDataExtension().TargetComponentTagMatch.Count <= 0)
                    {
                        return true;
                    }

                    if (targetCollection == StatisticEffectData.TargetCollection.NotSet && !(target is AbstractActor))
                    {
                        return true;
                    }

                    if (targetCollection == StatisticEffectData.TargetCollection.NotSet &&
                        target is AbstractActor actor)
                    {
                        if (effectData.getStatDataExtension().TargetComponentTagMatch.Overlaps(actor.GetTags()))
                            list.Add(target.StatCollection);
                    }

                    if (targetCollection == StatisticEffectData.TargetCollection.Pilot)
                    {
                        if (target.IsPilotable)
                        {
                            var pilot = target.GetPilot();
                            if (effectData.getStatDataExtension().TargetComponentTagMatch
                                .Overlaps(pilot.pilotDef.PilotTags))
                                list.Add(target.GetPilot().StatCollection);
                        }
                    }

                    else
                    {
                        List<MechComponent> targetComponents = GetTargetComponentsMatchingTags(target, targetCollection,
                            targetWeaponSubType, targetWeaponType, targetWeaponCategoryValue, targetAmmoCategoryValue,
                            effectData.getStatDataExtension().TargetComponentTagMatch);
                        for (int i = 0; i < targetComponents.Count; i++)
                        {
                            list.Add(targetComponents[i].StatCollection);
                        }
                    }

                    __result = list;
                    return false;
                }
            }
        }
    }
}
