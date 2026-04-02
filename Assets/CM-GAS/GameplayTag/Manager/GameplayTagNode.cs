using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CMGAS
{
    public class GameplayTagNode
    {
        public GameplayTagNode() { }
        public GameplayTagNode(CMName inTag, CMName inFullTag, GameplayTagNode inParentNode,
            bool inIsExplicitTag, bool inIsRestrictedTag, bool inAllowNonRestrictedChildren)
        {
            this.tag = inTag;
            this.parentNode = inParentNode;

            completeTagWithParents.list_gameplayTag.Add(new GameplayTag(inFullTag));
            GameplayTagNode rawParentNode = parentNode;

            if (rawParentNode != null && rawParentNode.GetSimpleTagName() != null)
            {
                GameplayTagContainer parentContinater = rawParentNode.GetSingleTagContainer();

                completeTagWithParents.list_parentTag.Add(parentContinater.list_gameplayTag[0]);
                completeTagWithParents.list_parentTag.AddRange(parentContinater.list_parentTag);
            }
        }

        private CMName tag;
        private GameplayTagContainer completeTagWithParents = new();
        private List<GameplayTagNode> list_childTag = new();
        public List<GameplayTagNode> List_childTag { get { return list_childTag; } private set { } }
        private GameplayTagNode parentNode = new();

        public CMName GetSimpleTagName() { return tag; }
        public GameplayTagContainer GetSingleTagContainer() { return completeTagWithParents; }

        public GameplayTag GetCompleteTag() { return completeTagWithParents.Num() > 0 ? 
                completeTagWithParents.list_parentTag[0] : GameplayTag.emptyTag; }
        public CMName GetCompleteTagName() { return GetCompleteTag().GetTagName(); }
        public CMString GetCompleteTagString() { return GetCompleteTag().ToString(); }
        public List<GameplayTagNode> GetChildTagNodes() { return list_childTag; }
        public GameplayTagNode GetParentTagNode() { return parentNode; }

#if UNITY_EDITOR
        private bool isExplicitTag = false;
        private bool allowNonRestrictedChildren = false;
        private bool isRestrictedTag = false;
#endif

        public bool IsExplicitTag()
        {
#if UNITY_EDITOR
            return isExplicitTag;
#endif
            return true;
        }
        public bool GetAllowNonRestrictedChildren()
        {
#if UNITY_EDITOR
            return allowNonRestrictedChildren;
#endif
            return true;
        }
        public bool IsRestrictedGameplayTag()
        {
#if UNITY_EDITOR
            return isRestrictedTag;
#endif
            return true;
        }


    }
}

