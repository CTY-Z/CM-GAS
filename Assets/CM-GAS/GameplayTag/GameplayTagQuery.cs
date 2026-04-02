using CMGAS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace CMGAS
{
    public class GameplayTagQuery
    {
        public GameplayTagQuery() { }
        public GameplayTagQuery(GameplayTagQuery Other)
        {

        }

        private int tokenStreamVersion;
        private List<GameplayTag> list_gameplayTagDic = new();
        private List<UInt16> list_queryTokenStream = new();
        public List<UInt16> List_queryTokenStream { get { return list_queryTokenStream; } }
        private CMString userDescription;
        private CMString autoDescription;

        public static bool operator ==(GameplayTagQuery query, GameplayTagQuery other)
        {
            return query.tokenStreamVersion == other.tokenStreamVersion &&
                query.list_gameplayTagDic == other.list_gameplayTagDic &&
                query.list_queryTokenStream == other.list_queryTokenStream &&
                query.userDescription == other.userDescription &&
                query.autoDescription == other.autoDescription;
        }
        public static bool operator !=(GameplayTagQuery query, GameplayTagQuery other)
        {
            return !(query == other);
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

        private static GameplayTagQuery emptyQuery;

        //friend
        public GameplayTag GetTagFromIndex(int TagIdx)
        {
            if (!list_gameplayTagDic.IsValidIndex(TagIdx))
            {
                CMDebug.Error("[GameplayTagQuery.GetTagFromIndex] - idx is invalid");
                return new GameplayTag();
            }

            return list_gameplayTagDic[TagIdx];
        }

        public void ReplaceFast(GameplayTagContainer container)
        {
            CMDebug.Ensure(container.list_gameplayTag.Count != list_gameplayTagDic.Count,
                "[GameplayTagQuery.ReplaceFast] - container.list_gameplayTag.Count != list_gameplayTagDic.Count");

            list_gameplayTagDic.Clear();
            list_gameplayTagDic.AddRange(container.list_gameplayTag);
        }
        public void ReplaceFast(GameplayTag tag)
        {
            CMDebug.Ensure(1 != list_gameplayTagDic.Count,
                "[GameplayTagQuery.ReplaceFast] - container.list_gameplayTag.Count != list_gameplayTagDic.Count");

            list_gameplayTagDic.Clear();
            list_gameplayTagDic.Add(tag);
        }

        public bool Matches(GameplayTagContainer container)
        {
            if (IsEmpty()) return false;

            QueryEvaluator qe = new(this);
            return qe.Eval(container);
        }

        public bool IsEmpty()
        {
            return (list_queryTokenStream.Count == 0);
        }

        public void Clear()
        {
            tokenStreamVersion = 0;
            list_gameplayTagDic = new();
            list_queryTokenStream = new();
            userDescription = "";
            autoDescription = "";
        }

        public void Build(GameplayTagQueryExpression rootQueryExpr, string inUserDescription = "")
        {
            tokenStreamVersion = (int)GameplayTagQueryStreamVersion.LatestVersion;
            userDescription = inUserDescription;

            list_queryTokenStream.Clear();
            list_queryTokenStream.Capacity = 128;
            list_gameplayTagDic.Clear();

            list_queryTokenStream.Add((int)GameplayTagQueryStreamVersion.LatestVersion);

            list_queryTokenStream.Add(1);
            rootQueryExpr.EmitTokens(list_queryTokenStream, list_gameplayTagDic);
        }

        public void GetQueryExpr(GameplayTagQueryExpression outExpr)
        {
            QueryEvaluator qe = new(this);
            qe.Read(outExpr);
        }

        public List<GameplayTag> GetGameplayTagList()
        {
            return list_gameplayTagDic;
        }

        #region STATIC

        public static GameplayTagQuery BuildQuery(GameplayTagQueryExpression rootQueryExpr, string inDescription = "")
        {
            GameplayTagQuery q = new GameplayTagQuery();
            q.Build(rootQueryExpr, inDescription);
            return q;
        }

        public static GameplayTagQuery MakeQuery_MatchAnyTags(GameplayTagContainer container)
        {
            return GameplayTagQuery.BuildQuery(new GameplayTagQueryExpression().AnyTagsMatch().AddTags(container));
        }

        public static GameplayTagQuery MakeQuery_MatchAllTags(GameplayTagContainer container)
        {
            return GameplayTagQuery.BuildQuery(new GameplayTagQueryExpression().AllTagsMatch().AddTags(container));
        }

        public static GameplayTagQuery MakeQuery_MatchNoTags(GameplayTagContainer container)
        {
            return GameplayTagQuery.BuildQuery(new GameplayTagQueryExpression().NoTagsMatch().AddTags(container));
        }

        public static GameplayTagQuery MakeQuery_MatchTag(GameplayTag tag)
        {
            return GameplayTagQuery.BuildQuery(new GameplayTagQueryExpression().AllTagsMatch().AddTag(tag));
        }

        #endregion


    }
}
