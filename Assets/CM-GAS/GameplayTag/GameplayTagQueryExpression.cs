using CMGAS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;

namespace CMGAS
{
    public class GameplayTagQueryExpression
    {
        //firend
        public GameplayTagQueryExprType exprType;
        List<GameplayTag> list_tagSet = new();
        List<GameplayTagQueryExpression> list_exprSet = new();

        public GameplayTagQueryExpression AnyTagsMatch()
        {
            exprType = GameplayTagQueryExprType.AnyTagsMatch;
            return this;
        }

        public GameplayTagQueryExpression AllTagsMatch()
        {
            exprType = GameplayTagQueryExprType.AllTagsMatch;
            return this;
        }

        public GameplayTagQueryExpression NoTagsMatch()
        {
            exprType = GameplayTagQueryExprType.NoTagsMatch;
            return this;
        }

        public GameplayTagQueryExpression AnyExprMatch()
        {
            exprType = GameplayTagQueryExprType.AnyExprMatch;
            return this;
        }

        public GameplayTagQueryExpression AllExprMatch()
        {
            exprType = GameplayTagQueryExprType.AllExprMatch;
            return this;
        }

        public GameplayTagQueryExpression NoExprMatch()
        {
            exprType = GameplayTagQueryExprType.NoExprMatch;
            return this;
        }

        public bool UsesTagSet()
        {
            return (exprType == GameplayTagQueryExprType.AllTagsMatch)
                || (exprType == GameplayTagQueryExprType.AnyTagsMatch)
                || (exprType == GameplayTagQueryExprType.NoTagsMatch);
        }
        public bool UsesExprSet()
        {
            return (exprType == GameplayTagQueryExprType.AllExprMatch)
                || (exprType == GameplayTagQueryExprType.AnyExprMatch)
                || (exprType == GameplayTagQueryExprType.NoExprMatch);
        }

        public GameplayTagQueryExpression AddTag(string tagString)
        {
            return AddTag(new CMName(tagString));
        }

        public GameplayTagQueryExpression AddTag(CMName tagName)
        {
            GameplayTag tag = GameplayTagsManager.instance.RequestGameplayTag(tagName);
            return AddTag(tag);
        }

        public GameplayTagQueryExpression AddTag(GameplayTag tag)
        {
            CMDebug.Ensure(UsesTagSet(), "[GameplayTagQueryExpression.AddTag] - this expression doesn't uses the tag data");
            list_tagSet.Add(tag);
            return this;
        }

        public GameplayTagQueryExpression AddTags(GameplayTagContainer container)
        {
            CMDebug.Ensure(UsesTagSet(), "[GameplayTagQueryExpression.AddTag] - this expression doesn't uses the tag data");
            list_tagSet.AddRange(container.list_gameplayTag);
            return this;
        }

        public GameplayTagQueryExpression AddExpr(GameplayTagQueryExpression expr)
        {
            CMDebug.Ensure(UsesExprSet(), "[GameplayTagQueryExpression.AddExpr] - this expression doesn't uses the expression list data");
            list_exprSet.Add(expr);
            return this;
        }

        public void EmitTokens(List<UInt16> list_tokenStream, List<GameplayTag> list_gameplayTag)
        {
            list_tokenStream.Add((UInt16)exprType);

            switch (exprType)
            {
                case GameplayTagQueryExprType.AnyTagsMatch:
                case GameplayTagQueryExprType.AllTagsMatch:
                case GameplayTagQueryExprType.NoTagsMatch:

                    UInt16 numTags = (UInt16)list_tagSet.Count;
                    list_tokenStream.Add(numTags);

                    foreach (var tag in list_tagSet)
                    {
                        int tagIdx = list_gameplayTag.AddUnique(tag);
                        if (tagIdx > 254)
                        {
                            CMDebug.Error("[GameplayTagQueryExpression.EmitTokens] - tagIdx: " + tagIdx + " > 254");
                            return;
                        }
                        list_tokenStream.Add((UInt16)tagIdx);
                    }
                    break;

                case GameplayTagQueryExprType.AnyExprMatch:
                case GameplayTagQueryExprType.AllExprMatch:
                case GameplayTagQueryExprType.NoExprMatch:

                    UInt16 numExprs = (UInt16)list_exprSet.Count;
                    list_tokenStream.Add(numExprs);

                    foreach (var e in list_exprSet)
                        e.EmitTokens(list_tokenStream, list_gameplayTag);

                    break;
                default:
                    break;
            }
        }
    }
}
