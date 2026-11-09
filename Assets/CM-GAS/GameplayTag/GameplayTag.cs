using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

namespace CMGAS
{
    [Serializable]
    public readonly struct GameplayTag : IEquatable<GameplayTag>, IComparable<GameplayTag>
    {
        public GameplayTag(CMName tagName) => this.tagName = tagName;

        public readonly CMName tagName;
        public static GameplayTag emptyTag;

        public override int GetHashCode() => tagName.GetHashCode();
        public override string ToString() => tagName.ToString();
        public CMName GetTagName() => tagName;
        public bool IsValid() => tagName.IsValid;
        public static bool operator ==(GameplayTag tag, GameplayTag other) => tag.tagName == other.tagName;
        public static bool operator !=(GameplayTag tag, GameplayTag other) => tag.tagName != other.tagName;

        public static GameplayTag RequestGameplayTag(CMName name, bool errorIfNotFound = true)
        {
            return GameplayTagsManager.instance.RequestGameplayTag(name, errorIfNotFound);
        }

        public static bool IsValidGameplayTagString(string tagString, string outError = "", string outFixedString = "")
        {
            return GameplayTagsManager.instance.IsValidGameplayTagString(tagString, outError, outFixedString);
        }

        public bool MatchesTag(GameplayTag tagToCheck)
        {
            GameplayTagContainer container = GameplayTagsManager.instance.GetSingleTagContainer(this);

            if (container != null)
                return container.HasTag(tagToCheck);

            return false;
        }

        public bool MatchesTagExact(GameplayTag tagToCheck)
        {
            if (!tagToCheck.IsValid())
                return false;

            return tagName == tagToCheck.tagName;
        }

        public bool MatchesAny(GameplayTagContainer containerToCheck)
        {
            GameplayTagContainer container = GameplayTagsManager.instance.GetSingleTagContainer(this);

            if (container != null)
                return container.HasAny(containerToCheck);

            return false;
        }

        public bool MatchesAnyExact(GameplayTagContainer containerToCheck)
        {
            GameplayTagContainer container = GameplayTagsManager.instance.GetSingleTagContainer(this);

            if (container.IsEmpty())
                return false;

            return containerToCheck.list_gameplayTag.Contains(this);
        }

        public int MatchesTagDepth(GameplayTag tagToCheck)
        {
            return GameplayTagsManager.instance.GameplayTagsMatchDepth(this, tagToCheck);
        }

        //SerializeFromMismatchedTag

        public GameplayTag RequestDirectParent()
        {
            return GameplayTagsManager.instance.RequestGameplayTagDirectParent(this);
        }

        public override bool Equals(object other) => other is GameplayTag && Equals(other);
        public bool Equals(GameplayTag other) => Equals(other);

        public int CompareTo(GameplayTag other) => tagName.CompareTo(other.tagName);

        //NetSerialize
        //PostSerialize
        //NetSerialize_Packed
        //SerializeFromMismatchedTag
        //FromExportString

    }
}