using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
    public interface IRemoveTag
    {
        public bool I_RemoveTagByExplicitName(CMName tagName);
    }

    public class GameplayTagContainer : IRemoveTag
    {
        public GameplayTagContainer() { }
        public GameplayTagContainer(GameplayTag tag)
        {
            AddTag(tag);
        }
        public GameplayTagContainer(GameplayTagContainer other)
        {
            list_gameplayTag.MoveTemp(other.list_gameplayTag);
            list_parentTag.MoveTemp(other.list_parentTag);
        }

        //CreateFromArray

        public List<GameplayTag> list_gameplayTag = new();
        public List<GameplayTag> list_parentTag = new();

        public bool HasTag(GameplayTag tag)
        {
            if(!tag.IsValid())
                return false;

            return list_gameplayTag.Contains(tag) || list_parentTag.Contains(tag);
        }
        public bool HasTagExact(GameplayTag tag)
        {
            if (!tag.IsValid())
                return false;

            return list_gameplayTag.Contains(tag);
        }
        public bool HasAny(GameplayTagContainer container)
        {
            if (container.IsEmpty())
                return false;

            foreach(var item in container.list_gameplayTag)
            {
                if(list_gameplayTag.Contains(item) || list_parentTag.Contains(item))
                    return true;
            }

            return false;
        }
        public bool HasAnyExact(GameplayTagContainer container)
        {
            if (container.IsEmpty())
                return false;

            foreach (var item in container.list_gameplayTag)
            {
                if (list_gameplayTag.Contains(item))
                    return true;
            }

            return false;
        }
        public bool HasAll(GameplayTagContainer container)
        {
            if (container.IsEmpty())
                return false;

            foreach (var item in container.list_gameplayTag)
            {
                if (!list_gameplayTag.Contains(item) && !list_parentTag.Contains(item))
                    return false;
            }

            return true;
        }
        public bool HasAllExact(GameplayTagContainer container)
        {
            if (container.IsEmpty())
                return false;

            foreach (var item in container.list_gameplayTag)
            {
                if (!list_gameplayTag.Contains(item))
                    return false;
            }

            return true;
        }
        public int Num()
        {
            return list_gameplayTag.Count;
        }
        public bool IsValid()
        {
            return list_gameplayTag.Count > 0;
        }
        public bool IsEmpty()
        {
            return list_gameplayTag.Count == 0;
        }
        public static bool operator ==(GameplayTagContainer container, GameplayTagContainer other)
        {
            if (container.list_gameplayTag.Count != other.list_gameplayTag.Count)
                return false;

            return container.HasAllExact(other);
        }
        public static bool operator !=(GameplayTagContainer container, GameplayTagContainer other)
        {
            return !(container == other);
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override string ToString()
        {
            return base.ToString();
        }

        public GameplayTagContainer GetGameplayTagParents()
        {
            GameplayTagContainer container = new();
            container.list_gameplayTag = list_gameplayTag;

            foreach(var tag in list_parentTag)
                container.list_gameplayTag.AddUnique(tag);

            return container;
        }

        public GameplayTagContainer Filter(GameplayTagContainer other)
        {
            GameplayTagContainer container = new();
            foreach(var tag in list_gameplayTag)
            {
                if(tag.MatchesAny(other))
                    container.AddTagFast(tag);
            }
            return container;
        }

        public GameplayTagContainer FilterExact(GameplayTagContainer other)
        {
            GameplayTagContainer container = new();
            foreach (var tag in list_gameplayTag)
            {
                if (tag.MatchesAnyExact(other))
                    container.AddTagFast(tag);
            }
            return container;
        }

        public bool MatchesQuery(GameplayTagQuery query)
        {
            return query.Matches(this);
        }

        public void AppendTags(GameplayTagContainer container)
        {
            list_gameplayTag.Capacity = list_gameplayTag.Count + container.list_gameplayTag.Count;
            list_parentTag.Capacity = list_parentTag.Count + container.list_parentTag.Count;

            foreach(var tag in container.list_gameplayTag)
                list_gameplayTag.AddUnique(tag);

            foreach (var tag in container.list_parentTag)
                list_parentTag.AddUnique(tag);
        }

        public void AppendMatchingTags(GameplayTagContainer containerA, GameplayTagContainer containerB)
        {
            foreach(var tagA in containerA.list_gameplayTag)
            {
                if(tagA.MatchesAny(containerB))
                    AddTag(tagA);
            }
        }

        public void AddTag(GameplayTag tagToAdd)
        {

            if (tagToAdd.IsValid())
            {
                list_gameplayTag.AddUnique(tagToAdd);
                AddParentsForTag(tagToAdd);
            }
        }

        public void AddTagFast(GameplayTag tagToAdd)
        {
            list_gameplayTag.Add(tagToAdd);
            AddParentsForTag(tagToAdd);
        }

        public bool AddLeafTag(GameplayTag tagToAdd)
        {
            if(HasTagExact(tagToAdd))
                return true;

            if (HasTag(tagToAdd))
                return false;

            GameplayTagContainer container = GameplayTagsManager.instance.GetSingleTagContainer(tagToAdd);

            if(container == null)
                return false;

            foreach(var parentTag in container.list_parentTag)
            {
                if(HasTagExact(parentTag))
                    RemoveTag(parentTag);
            }

            AddTag(tagToAdd);
            return true;
        }

        public bool RemoveTag(GameplayTag tagToRemovem, bool bDeferParentTags = false)
        {
            int idx = list_gameplayTag.RemoveSingle(tagToRemovem);

            if(idx > 0)
            {
                if(!bDeferParentTags)
                    FillParentTags();

                return true;
            }
            return false;
        }

        public void RemoveTags(GameplayTagContainer tagsToRemove)
        {
            int numChanged = 0;

            foreach(var tag in tagsToRemove.list_gameplayTag)
                numChanged += list_gameplayTag.RemoveSingle(tag);

            if(numChanged > 0)
                FillParentTags();
        }

        public void Reset()
        {
            list_gameplayTag.Clear();
            list_parentTag.Clear();
        }

        //Serialize
        //NetSerialize
        //ImportTextItem
        //PostScriptConstruct
        //FromExportString
        //ToMatchingText

        public List<GameplayTag> GetGameplayTagArray()
        {
            return list_gameplayTag;
        }

        public void GetGameplayTagArray(out List<GameplayTag> list_gameplayTag)
        {
            list_gameplayTag = this.list_gameplayTag;
        }

        public bool IsValidIndex(int idx)
        {
            return list_gameplayTag.IsValidIndex(idx);
        }

        public GameplayTag GetByIdx(int idx)
        {
            if(IsValidIndex(idx)) return list_gameplayTag[idx];

            return new GameplayTag();
        }

        public GameplayTag First()
        {
            return list_gameplayTag.Count > 0 ? list_gameplayTag[0] : new GameplayTag();
        }

        public GameplayTag Last()
        {
            return list_gameplayTag.Count > 0 ? list_gameplayTag[list_gameplayTag.Count] : new GameplayTag();
        }

        public void FillParentTags()
        {
            list_parentTag.Clear();

            foreach(var tag in list_gameplayTag)
                AddParentsForTag(tag);
        }

        [Obsolete]
        public bool HasTagFast(GameplayTag tagToCheck, GameplayTagMatchType tagMatchType, GameplayTagMatchType tagToCheckMatchType)
        {
            bool result;
            if(tagToCheckMatchType == GameplayTagMatchType.Explicit)
            {
                result = list_gameplayTag.Contains(tagToCheck);

                if (!result && tagMatchType == GameplayTagMatchType.IncludeParentTags)
                    result = list_parentTag.Contains(tagToCheck);
            }
            else
                result = ComplexHasTag(tagToCheck, tagMatchType, tagToCheckMatchType);

            return result;
        }

        [Obsolete]
        public bool ComplexHasTag(GameplayTag tagToCheck, GameplayTagMatchType tagMatchType, GameplayTagMatchType tagToCheckMatchType)
        {
            return true;
        }

        public bool RemoveTagByExplicitName(CMName tagName)
        {
            foreach(var tag in list_gameplayTag)
            {
                if(tag.GetTagName() == tagName)
                {
                    RemoveTag(tag);
                    return true;
                }
            }

            return false;
        }
        bool IRemoveTag.I_RemoveTagByExplicitName(CMName tagName)
        {
            return RemoveTagByExplicitName(tagName);
        }

        public void AddParentsForTag(GameplayTag tag)
        {
            GameplayTagContainer container = GameplayTagsManager.instance.GetSingleTagContainer(tag);
            if (container != null)
            {
                foreach (GameplayTag parentTag in container.list_parentTag)
                    list_parentTag.AddUnique(parentTag);
            }
        }
    }
}

