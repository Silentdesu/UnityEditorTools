using System;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace EditorTools
{
    public static class AddressablesUtils
    {
        public static AddressableAssetGroup GetOrCreateGroup(string groupName)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogError("Addressables settings is null");
                return null;
            }
            
            AddressableAssetGroup group = settings.FindGroup(groupName);

            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName, 
                    setAsDefaultGroup: false, 
                    readOnly: false, 
                    postEvent: false,
                    schemasToCopy: null,
                    typeof(BundledAssetGroupSchema));
            }

            return group;
        }
    }
}