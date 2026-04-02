using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
    public class GameplayTagsManager : CMSingleton<GameplayTagsManager>
    {
        private Dictionary<GameplayTag, GameplayTagNode> dic_tag_node = new();

        public void RequestGameplayTagContainer(List<string> list_tagString, GameplayTagContainer container, bool errorIfNotFound = true)
        {
            foreach (var str in list_tagString)
            {
                GameplayTag resquestedTag = RequestGameplayTag(new CMName(str.TrimStart().TrimEnd()), errorIfNotFound);
                if (resquestedTag.IsValid())
                    container.AddTag(resquestedTag);
            }
        }

        static HashSet<CMName> set_CMName = new HashSet<CMName>();
        public GameplayTag RequestGameplayTag(CMName tagName, bool errorIfNotFound = true)
        {
            GameplayTag possibleTag = new(tagName);

            if(dic_tag_node.ContainsKey(possibleTag))
                return possibleTag;
            else if(errorIfNotFound)
            {
                if(!set_CMName.Contains(tagName))
                {
                    CMDebug.Error("Requested Gameplay Tag [" + tagName.ToString() + "] was not found, tags must be loaded from config or registered as a native tag");
                    set_CMName.Add(tagName);
                }
            }

            return new GameplayTag();
        }

        public bool IsValidGameplayTagString(string tagString, string outError = "", string outFixedString = "")
        {
            bool isVaild = true;
            string fixedString = tagString;
            string errorTxt;

            if(fixedString == "")
            {
                errorTxt = "EmptyStringError : Tag is empty";
                isVaild = false;
            }

            return true;
        }

        public GameplayTagContainer GetSingleTagContainer(GameplayTag tag)
        {
            //todo
            return null;
        }

        public int GameplayTagsMatchDepth(GameplayTag tagOne, GameplayTag tagTwo)
        {
            //todo
            return 0;
        }

        public GameplayTag RequestGameplayTagDirectParent(GameplayTag tag)
        {
            //todo
            return new GameplayTag();
        }
    }
}
