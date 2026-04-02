using CMGAS;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;

namespace CMGAS
{
    public class QueryEvaluator
    {
        private GameplayTagQuery query;
        private int curStreamIdx;
        private int version;
        private bool readError;

        public QueryEvaluator(GameplayTagQuery q)
        {
            query = q;
            curStreamIdx = 0;
            version = (int)GameplayTagQueryStreamVersion.LatestVersion;
            readError = false;
        }

        public bool Eval(GameplayTagContainer container)
        {
            curStreamIdx = 0;

            version = GetToken();
            if (readError)
                return false;

            bool ret = false;
            UInt16 hasRootExpr = GetToken();
            if (!readError && (hasRootExpr != 0))
                ret = EvalExpr(container);

            CMDebug.Ensure(curStreamIdx == query.List_queryTokenStream.Count, "[QueryEvaluator.Eval] - curStreamIdx != query.List_queryTokenStream.Count");
            return ret;
        }

        public void Read(GameplayTagQueryExpression outExpr)
        {
            outExpr = new();
            curStreamIdx = 0;

            if (query.List_queryTokenStream.Count > 0)
            {
                version = GetToken();
                if (readError)
                {
                    UInt16 hasRootExpression = GetToken();
                    if (!readError && (hasRootExpression != 0))
                        ReadExpr(outExpr);
                }
            }

            CMDebug.Ensure(curStreamIdx == query.List_queryTokenStream.Count, "[QueryEvaluator.Eval] - curStreamIdx != query.List_queryTokenStream.Count");
        }

        public void ReadExpr(GameplayTagQueryExpression expr)
        {
            expr.exprType = (GameplayTagQueryExprType)GetToken();
            if (readError)
                return;

            if (expr.UsesTagSet())
            {
                int tagIdx = GetToken();
                if (readError)
                    return;

                GameplayTag tag = query.GetTagFromIndex(tagIdx);
                expr.AddTag(tag);
            }
            else
            {
                int numExpr = GetToken();
                if (readError)
                    return;

                for (int i = 0; i < numExpr; ++i)
                {
                    GameplayTagQueryExpression tempExpr = new();
                    ReadExpr(tempExpr);
                    expr.AddExpr(tempExpr);
                }
            }
        }

        public bool EvalAnyTagsMatch(GameplayTagContainer container, bool isSkip)
        {
            bool shortCircuit = isSkip;
            bool result = false;

            int numTags = GetToken();
            if (readError)
                return false;

            for (int i = 0; i < numTags; ++i)
            {
                int tagIdx = GetToken();
                if (readError)
                    return false;

                if (shortCircuit == false)
                {
                    GameplayTag tag = query.GetTagFromIndex(tagIdx);
                    bool hasTag = container.HasTag(tag);

                    if (hasTag)
                    {
                        shortCircuit = true;
                        result = true;
                    }
                }
            }

            return result;
        }

        public bool EvalAllTagsMatch(GameplayTagContainer container, bool isSkip)
        {
            bool shortCircuit = isSkip;
            bool result = true;

            int numTags = GetToken();
            if (readError)
                return false;

            for (int i = 0; i < numTags; ++i)
            {
                int tagIdx = GetToken();
                if (readError)
                    return false;

                if (shortCircuit == false)
                {
                    GameplayTag tag = query.GetTagFromIndex(tagIdx);
                    bool hasTag = container.HasTag(tag);

                    if (hasTag == false)
                    {
                        shortCircuit = true;
                        result = false;
                    }
                }
            }

            return result;
        }

        public bool EvalNoTagsMatch(GameplayTagContainer container, bool isSkip)
        {
            bool shortCircuit = isSkip;
            bool result = true;

            int numTags = GetToken();
            if (readError)
                return false;

            for (int i = 0; i < numTags; ++i)
            {
                int TagIdx = GetToken();
                if (readError)
                    return false;

                if (shortCircuit == false)
                {
                    GameplayTag tag = query.GetTagFromIndex(TagIdx);
                    bool hasTag = container.HasTag(tag);

                    if (hasTag == true)
                    {
                        shortCircuit = true;
                        result = false;
                    }
                }
            }

            return result;
        }

        public bool EvalAnyExprMatch(GameplayTagContainer container, bool isSkip)
        {
            bool shortCircuit = isSkip;
            bool result = false;

            int numTags = GetToken();
            if (readError)
                return false;

            for (int i = 0; i < numTags; ++i)
            {
                int TagIdx = GetToken();
                if (readError)
                    return false;

                bool exprResult = EvalExpr(container, shortCircuit);
                if (shortCircuit == false)
                {
                    if (exprResult == true)
                    {
                        result = true;
                        shortCircuit = true;
                    }
                }
            }

            return result;
        }

        public bool EvalAllExprMatch(GameplayTagContainer container, bool isSkip)
        {
            bool shortCircuit = isSkip;
            bool result = true;

            int numTags = GetToken();
            if (readError)
                return false;

            for (int i = 0; i < numTags; ++i)
            {
                bool exprResult = EvalExpr(container, shortCircuit);
                if (shortCircuit == false)
                {
                    if (exprResult == false)
                    {
                        result = false;
                        shortCircuit = true;
                    }
                }
            }

            return result;
        }

        public bool EvalNoExprMatch(GameplayTagContainer container, bool isSkip)
        {
            bool shortCircuit = isSkip;
            bool result = true;

            int numTags = GetToken();
            if (readError)
                return false;

            for (int i = 0; i < numTags; ++i)
            {
                bool exprResult = EvalExpr(container, shortCircuit);
                if (shortCircuit == false)
                {
                    if (exprResult == true)
                    {
                        result = false;
                        shortCircuit = true;
                    }
                }
            }

            return result;
        }

        public bool EvalExpr(GameplayTagContainer container, bool isSkip = false)
        {
            GameplayTagQueryExprType exprType = (GameplayTagQueryExprType)GetToken();
            if (readError)
                return false;

            switch (exprType)
            {
                case GameplayTagQueryExprType.AnyTagsMatch: return EvalAnyTagsMatch(container, isSkip);
                case GameplayTagQueryExprType.AllTagsMatch: return EvalAllTagsMatch(container, isSkip);
                case GameplayTagQueryExprType.NoTagsMatch: return EvalNoTagsMatch(container, isSkip);

                case GameplayTagQueryExprType.AnyExprMatch: return EvalAnyExprMatch(container, isSkip);
                case GameplayTagQueryExprType.AllExprMatch: return EvalAllExprMatch(container, isSkip);
                case GameplayTagQueryExprType.NoExprMatch: return EvalNoExprMatch(container, isSkip);
            }


            return false;
        }

        private UInt16 GetToken()
        {
            if (query.List_queryTokenStream.IsValidIndex(curStreamIdx))
            {
                return query.List_queryTokenStream[curStreamIdx++];
            }

            CMDebug.Warning("[QueryEvaluator.GetToken] - Error parsing FGameplayTagQuery!");
            readError = false;
            return 0;
        }
    }
}
