// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    internal class SearchUtility
    {
        private static void RemoveUnwantedWhitespaces(ref string searchString)
        {
            // Some users add a whitespace after the colon (remove it)
            searchString = searchString.Replace(": ", ":");
        }

        // Supports the following syntax:
        // 't:type' syntax (e.g 't:Texture2D' will show Texture2D objects)
        // 'l:assetlabel' syntax (e.g 'l:architecture' will show assets with AssetLabel 'architecture')
        // 'ref[:id]:path' syntax (e.g 'ref:1234' will show objects that references the object with instanceID 1234)
        // 'v:versionState' syntax (e.g 'v:modified' will show objects that are modified locally)
        // 's:softLockState' syntax (e.g 's:inprogress' will show objects that are modified by anyone (except you))
        // 'a:area' syntax (e.g 'a:all' will s search in all assets, 'a:assets' will s search in assets folder only and 'a:packages' will s search in packages folder only)
        // 'glob:path' syntax (e.g 'glob:Assets/**/*.{png|PNG}' will show objects in any subfolder with name ending by .png or .PNG)
        internal static bool ParseSearchString(string searchText, SearchFilter filter)
        {
            if (string.IsNullOrEmpty(searchText))
                return false;

            filter.ClearSearch();
            filter.originalText = searchText;

            string searchString = string.Copy(searchText);
            RemoveUnwantedWhitespaces(ref searchString);

            bool parsed = false;

            // Split filter into separate words with space or tab as separators
            const string kFilterSeparator = " \t,*?";

            // Skip any separators preceding the filter
            int pos = FindFirstPositionNotOf(searchString, kFilterSeparator);
            if (pos == -1)
                pos = 0;
            while (pos < searchString.Length)
            {
                int endpos = searchString.IndexOfAny(kFilterSeparator.ToCharArray(), pos);

                if (endpos == -1)
                    endpos = searchString.Length;

                // Check if we have quotes (may be used for pathnames) inbetween start and a /filter-separator/
                int q1 = searchString.IndexOf('"', pos);
                int q2 = -1;
                if (q1 != -1 && q1 < endpos)
                {
                    q2 = searchString.IndexOf('"', q1 + 1);
                    if (q2 != -1 && q2 > endpos - 1)
                    {
                        // Advance to a /filter-separator/ after the quote
                        endpos = searchString.IndexOfAny(kFilterSeparator.ToCharArray(), q2);
                        if (endpos == -1)
                            endpos = searchString.Length;
                    }
                }

                if (endpos > pos)
                {
                    string token = searchString.Substring(pos, endpos - pos);

                    if (q1 != -1)
                        q1 -= pos;

                    if (q2 != -1)
                        q2 -= pos;

                    if (CheckForKeyWords(token, filter, q1, q2))
                        parsed = true;
                    else
                        filter.nameFilter += (string.IsNullOrEmpty(filter.nameFilter) ? "" : " ") + token; // force single space between name tokens
                }
                pos = endpos + 1;
            }
            return parsed;
        }

        internal static bool CheckForKeyWords(string searchString, SearchFilter filter, int quote1, int quote2)
        {
            bool parsed = false;

            // Support: 't:type' syntax (e.g 't:Texture2D' will show Texture2D objects)
            int index = searchString.IndexOf("t:");
            if (index == -1)
                index = searchString.IndexOf("t=");
            if (index == 0)
            {
                string type = searchString.Substring(index + 2);
                List<string> tmp = new List<string>(filter.classNames);
                tmp.Add(type);
                filter.classNames = tmp.ToArray();
                parsed = true;
            }

            // Support: 'l:assetlabel' syntax (e.g 'l:architecture' will show assets with AssetLabel 'architecture')
            index = searchString.IndexOf("l:");
            if (index == -1)
                index = searchString.IndexOf("l=");
            if (index == 0)
            {
                string label = searchString.Substring(index + 2);
                List<string> tmp = new List<string>(filter.assetLabels);
                tmp.Add(label);
                filter.assetLabels = tmp.ToArray();
                parsed = true;
            }


            // Support: 'a:area' syntax
            index = searchString.IndexOf("a:");
            if (index >= 0)
            {
                string areaString = searchString.Substring(index + 2);
                if (string.Compare(areaString, "all", true) == 0)
                {
                    filter.searchArea = SearchFilter.SearchArea.AllAssets;
                    parsed = true;
                }
                else if (string.Compare(areaString, "assets", true) == 0)
                {
                    filter.searchArea = SearchFilter.SearchArea.InAssetsOnly;
                    parsed = true;
                }
                else if (string.Compare(areaString, "packages", true) == 0)
                {
                    filter.searchArea = SearchFilter.SearchArea.InPackagesOnly;
                    parsed = true;
                }
            }

            // Support: 'b:assetBundleName' syntax (e.g 'b:materialAssetBundle' will show assets within assetBundle 'materialAssetBundle')
            index = searchString.IndexOf("b:");
            if (index == 0)
            {
                string assetBundleName = searchString.Substring(index + 2);
                List<string> tmp = new List<string>(filter.assetBundleNames);
                tmp.Add(assetBundleName);
                filter.assetBundleNames = tmp.ToArray();
                parsed = true;
            }

            // Support: 'ref[:id]:path' syntax (e.g 'ref:1234' will show objects that references the object with instanceID 1234)
            index = searchString.IndexOf("ref:");
            if (index == 0)
            {
                int instanceID = 0;

                int firstColon = index + 3;
                int secondColon = searchString.IndexOf(':', firstColon + 1);
                if (secondColon >= 0)
                {
                    // Instead of resolving a passed-in pathname to an instance-id, use a supplied one.
                    // The pathname is effectively just a UI hint of whose references we're filtering out.
                    string refString = searchString.Substring(firstColon + 1, secondColon - firstColon - 1);
                    int id;
                    if (System.Int32.TryParse(refString, out id))
                        instanceID = id;
                    //else
                    //  Debug.Log ("Not valid refString to case to Integer " + refString); // outcomment for debugging
                }
                else
                {
                    string assetPath;
                    if (quote1 >= 0 && quote2 >= 0)
                    {
                        int startIndex = quote1 + 1;
                        int count = quote2 - quote1 - 1;
                        if (count < 0)
                            count = searchString.Length - startIndex;

                        // Strip filepath from quotes, don't prefix with Assets/, we need to support Packages/ (https://fogbugz.unity3d.com/f/cases/1161019/)
                        assetPath = searchString.Substring(startIndex, count);
                    }
                    else
                        // Otherwise use string from colon to end, don't prefix with Assets/, we need to support Packages/ (https://fogbugz.unity3d.com/f/cases/1161019/)
                        assetPath = searchString.Substring(firstColon + 1);

                    Object obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (obj == null)
                        // Backward compatibility, in case Assets/ was strip from path
                        obj = AssetDatabase.LoadMainAssetAtPath("Assets/" + assetPath);

                    if (obj != null)
                        instanceID = obj.GetInstanceID();
                    //else
                    //  Debug.Log ("Not valid assetPath " + assetPath); // outcomment for debugging
                }

                filter.referencingInstanceIDs = new[] { instanceID };
                parsed = true;
            }

            // Support: 'glob:path' syntax (e.g 'glob:Assets/**/*.{png|PNG}' will show objects in any subfolder with name ending by .png or .PNG)
            index = searchString.IndexOf("glob:");
            if (index == 0)
            {
                string globValue = searchString.Substring(5);
                if (quote1 >= 0 && quote2 >= 0)
                {
                    int startIndex = quote1 + 1;
                    int count = quote2 - quote1 - 1;
                    if (count < 0)
                        count = searchString.Length - startIndex;
                    globValue = searchString.Substring(startIndex, count);
                }
                var globs = new List<string>(filter.globs);
                globs.Add(globValue);
                filter.globs = globs.ToArray();
                parsed = true;
            }

            var importSearchToken = $"{ImportLog.Filters.SearchToken}:";
            index = searchString.IndexOf(importSearchToken);
            if (index == 0)
            {
                var label = searchString.Substring(importSearchToken.Length);
                if (label == ImportLog.Filters.AllIssuesStr)
                {
                    filter.importLogFlags = ImportLogFlags.Error | ImportLogFlags.Warning;
                    parsed = true;
                }
                else if (label == ImportLog.Filters.ErrorsStr)
                {
                    filter.importLogFlags = ImportLogFlags.Error;
                    parsed = true;
                }
                else if (label == ImportLog.Filters.WarningsStr)
                {
                    filter.importLogFlags = ImportLogFlags.Warning;
                    parsed = true;
                }
            }

            return parsed;
        }

        private static int FindFirstPositionNotOf(string source, string chars)
        {
            if (source == null) return -1;
            if (chars == null) return 0;
            if (source.Length == 0) return -1;
            if (chars.Length == 0) return 0;

            for (int i = 0; i < source.Length; i++)
            {
                if (chars.IndexOf(source[i]) == -1)
                    return i;
            }
            return -1;
        }
    }
}
