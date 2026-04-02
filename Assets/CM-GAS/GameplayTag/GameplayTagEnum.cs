using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
    public enum GameplayTagMatchType
    {
        Explicit,           // This will check for a match against just this tag
        IncludeParentTags,	// This will also check for matches against all parent tags
    }

    public enum GameplayTagQueryStreamVersion
    {
        InitialVersion = 0,

        // -----<new versions can be added before this line>-------------------------------------------------
        // - this needs to be the last line (see note below)
        VersionPlusOne,
        LatestVersion = VersionPlusOne - 1
    }

    public enum GameplayTagQueryExprType
    {
        Undefined = 0,
        AnyTagsMatch,
        AllTagsMatch,
        NoTagsMatch,
        AnyExprMatch,
        AllExprMatch,
        NoExprMatch
    }

    //GameplayTagManager 

    public enum GameplayTagSourceType
    {
        Native,				// Was added from C++ code
	    DefaultTagList,		// The default tag list in DefaultGameplayTags.ini
	    TagList,			// Another tag list from an ini in tags/*.ini
	    RestrictedTagList,	// Restricted tags from an ini
	    DataTable,			// From a DataTable
	    Invalid,			// Not a real source
    };

    public enum GameplayTagSelectionType
    {
        None,
        NonRestrictedOnly,
        RestrictedOnly,
        All
    };
}

