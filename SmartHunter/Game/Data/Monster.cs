﻿using SmartHunter.Core;
using SmartHunter.Core.Data;
using SmartHunter.Game.Config;
using SmartHunter.Game.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SmartHunter.Game.Data
{
    public enum MonsterCrown
    {
        None,
        Mini,
        Silver,
        Gold
    }

    public class Monster : ChangeableVisibility
    {
        public ulong Address { get; private set; }

        string m_Id;
        public string Id
        {
            get { return m_Id; }
            set
            {
                if (SetProperty(ref m_Id, value))
                {
                    NotifyPropertyChanged(nameof(IsVisible));
                    UpdateLocalization();
                }
            }
        }

        public string Name
        {
            get
            {
                return LocalizationHelper.GetMonsterName(Id);
            }
        }

        float m_SizeScale;
        public float SizeScale
        {
            get { return m_SizeScale; }
            set
            {
                if (SetProperty(ref m_SizeScale, value))
                {
                    NotifyPropertyChanged(nameof(ModifiedSizeScale));
                    NotifyPropertyChanged(nameof(Size));
                    NotifyPropertyChanged(nameof(Crown));
                }
            }
        }

        public float ModifiedSizeScale
        {
            get
            {
                float modifiedSizeScale = SizeScale;

                MonsterConfig config = null;
                if (ConfigHelper.MonsterData.Values.Monsters.TryGetValue(Id, out config))
                {
                    modifiedSizeScale /= config.ScaleModifier;
                }

                return modifiedSizeScale;
            }
        }

        public float Size
        {
            get
            {
                float size = 0; 

                MonsterConfig config = null;
                if (ConfigHelper.MonsterData.Values.Monsters.TryGetValue(Id, out config))
                {
                    size = config.BaseSize * ModifiedSizeScale;
                }

                return size;
            }
        }

        public MonsterCrown Crown
        {
            get
            {
                MonsterCrown crown = MonsterCrown.None;

                MonsterConfig config = null;
                if (ConfigHelper.MonsterData.Values.Monsters.TryGetValue(Id, out config) && config.Crowns != null)
                {
                    float modifiedSizeScale = ModifiedSizeScale;

                    if (modifiedSizeScale <= config.Crowns.Mini)
                    {
                        crown = MonsterCrown.Mini;
                    }
                    else if (modifiedSizeScale >= config.Crowns.Gold)
                    {
                        crown = MonsterCrown.Gold;
                    }
                    else if (modifiedSizeScale >= config.Crowns.Silver)
                    {
                        crown = MonsterCrown.Silver;
                    }
                }

                return crown;
            }
        }

        public Progress Health { get; private set; }
        public ObservableCollection<MonsterPart> Parts { get; private set; }
        public ObservableCollection<MonsterPart> RemovableParts { get; private set; }
        public ObservableCollection<MonsterStatusEffect> StatusEffects { get; private set; }

        public Monster(ulong address, string id, float maxHealth, float currentHealth, float sizeScale)
        {
            Address = address;
            m_Id = id;
            Health = new Progress(maxHealth, currentHealth);
            m_SizeScale = sizeScale;

            Parts = new ObservableCollection<MonsterPart>();
            RemovableParts = new ObservableCollection<MonsterPart>();
            StatusEffects = new ObservableCollection<MonsterStatusEffect>();
        }

        public MonsterPart UpdateAndGetPart(ulong address, bool isRemovable, float maxHealth, float currentHealth, int timesBrokenCount)
        {
            ObservableCollection<MonsterPart> collection = Parts;
            if (isRemovable)
            {
                collection = RemovableParts;
            }

            MonsterPart part = collection.SingleOrDefault(collectionPart => collectionPart.Address == address);
            if (part != null)
            {
                part.IsRemovable = isRemovable;
                part.Health.Max = maxHealth;
                part.Health.Current = currentHealth;
                part.TimesBrokenCount = timesBrokenCount;
            }
            else
            {
                part = new MonsterPart(this, address, isRemovable, maxHealth, currentHealth, timesBrokenCount);
                part.Changed += PartOrStatusEffect_Changed;

                collection.Add(part);
            }

            part.UpdateVisibility();

            return part;
        }

        public MonsterStatusEffect UpdateAndGetStatusEffect(ulong address, int id, float maxBuildup, float currentBuildup, float maxDuration, float currentDuration, int timesActivatedCount)
        {
            MonsterStatusEffect statusEffect = StatusEffects.SingleOrDefault(collectionStatusEffect => collectionStatusEffect.Address == address);
            if (statusEffect != null)
            {
                statusEffect.Duration.Max = maxDuration;
                statusEffect.Duration.Current = maxDuration - currentDuration;
                statusEffect.Buildup.Max = maxBuildup;
                statusEffect.Buildup.Current = currentBuildup;
                statusEffect.TimesActivatedCount = timesActivatedCount;
            }
            else if (!ConfigHelper.MonsterData.Values.StatusEffects.ContainsKey(id))
            {
                return null;
            }
            else
            {
                statusEffect = new MonsterStatusEffect(this, address, id, maxBuildup, currentBuildup, maxDuration, currentDuration, timesActivatedCount);
                statusEffect.Changed += PartOrStatusEffect_Changed;

                StatusEffects.Add(statusEffect);
            }

            bool isValidId = ConfigHelper.MonsterData.Values.StatusEffects.ContainsKey(id);
            if (!isValidId)
            {
                statusEffect.IsVisible = false;
            }
            else
            {
                statusEffect.UpdateVisibility();
            }

            return statusEffect;
        }

        public void UpdateLocalization()
        {
            NotifyPropertyChanged(nameof(Name));

            foreach (var removablePart in RemovableParts)
            {
                removablePart.NotifyPropertyChanged(nameof(MonsterPart.Name));
            }
            foreach (var part in Parts)
            {
                part.NotifyPropertyChanged(nameof(MonsterPart.Name));
            }
            foreach (var statusEffect in StatusEffects)
            {
                statusEffect.NotifyPropertyChanged(nameof(MonsterPart.Name));
            }
        }

        public static bool IsIncluded(string monsterId)
        {
            return ConfigHelper.Main.Values.Overlay.MonsterWidget.MatchIncludeMonsterIdRegex(monsterId);
        }

        private void PartOrStatusEffect_Changed(object sender, GenericEventArgs<DateTimeOffset> e)
        {
            UpdateLastChangedTime();
        }
        
        public override void UpdateVisibility()
        {
            IsVisible = IsIncluded(Id) && CanShow(InitialTime, LastChangedTime, ConfigHelper.Main.Values.Overlay.MonsterWidget.ShowUnchangedMonsters, ConfigHelper.Main.Values.Overlay.MonsterWidget.HideMonstersAfterSeconds);
        }
    }
}
