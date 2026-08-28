using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Switches the round-image group between local content and content served over HTTPS.
    ///
    /// Remote mode also flips the play mode script to "Use Existing Build", because playing against
    /// the asset database would silently bypass the network and prove nothing.
    /// </summary>
    public static class AddressablesHosting
    {
        private const string RemoteGroupName = "RoundImages_Remote";
        private const string RemoteBuildPathValue = "ServerData/[BuildTarget]";
        private const string RemoteLoadPathValue = "https://silvedko.github.io/Coleplay_Test/[BuildTarget]";

        [MenuItem("Tools/Coleplay/Addressables/Use Remote Hosting")]
        public static void UseRemoteHosting()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressables settings are missing. Run Tools > Coleplay > Set Up Playable Scene first.");
                return;
            }

            var profileId = EnsureActiveProfile(settings);
            var profiles = settings.profileSettings;

            profiles.SetValue(profileId, AddressableAssetSettings.kRemoteBuildPath, RemoteBuildPathValue);
            profiles.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath, RemoteLoadPathValue);

            SetGroupPaths(settings, RemoteGroupName,
                AddressableAssetSettings.kRemoteBuildPath,
                AddressableAssetSettings.kRemoteLoadPath);

            // Without a remote catalog the player would keep reading the one baked into the build.
            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            SetPlayModeBuilder<BuildScriptPackedPlayMode>(settings);
            Save(settings);

            // Echo what the build will actually resolve, not what we intended to set.
            Debug.Log("Addressables switched to remote hosting."
                      + "\n  RemoteBuildPath = " + profiles.GetValueByName(profileId, AddressableAssetSettings.kRemoteBuildPath)
                      + "\n  RemoteLoadPath  = " + profiles.GetValueByName(profileId, AddressableAssetSettings.kRemoteLoadPath)
                      + "\n  Remote catalog  = " + settings.BuildRemoteCatalog
                      + "\nNow run Addressables Groups > Build > New Build > Default Build Script and publish ServerData.");
        }

        [MenuItem("Tools/Coleplay/Addressables/Use Local Content")]
        public static void UseLocalContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return;
            }

            SetGroupPaths(settings, RemoteGroupName,
                AddressableAssetSettings.kLocalBuildPath,
                AddressableAssetSettings.kLocalLoadPath);

            settings.BuildRemoteCatalog = false;

            SetPlayModeBuilder<BuildScriptFastMode>(settings);
            Save(settings);

            Debug.Log("Addressables switched back to local content and the asset database play mode.");
        }

        [MenuItem("Tools/Coleplay/Addressables/Clear Downloaded Bundle Cache")]
        public static void ClearBundleCache()
        {
            // Remote bundles are cached after the first run, so without this a second play session
            // would never touch the network and the hosting could not be verified.
            Debug.Log(Caching.ClearCache()
                ? "Bundle cache cleared."
                : "Bundle cache is in use and could not be cleared - leave play mode and try again.");
        }

        /// <summary>
        /// Addressables settings created from a script can end up with no active profile at all.
        /// Path variables then resolve to nothing, which is how a build silently produces bundles
        /// without a remote catalog. This makes sure exactly one profile is active before we write to it.
        /// </summary>
        private static string EnsureActiveProfile(AddressableAssetSettings settings)
        {
            var profiles = settings.profileSettings;

            if (!string.IsNullOrEmpty(settings.activeProfileId) &&
                !string.IsNullOrEmpty(profiles.GetProfileName(settings.activeProfileId)))
            {
                return settings.activeProfileId;
            }

            var names = profiles.GetAllProfileNames();
            var profileId = names.Count > 0
                ? profiles.GetProfileId(names[0])
                : profiles.AddProfile("Default", null);

            settings.activeProfileId = profileId;
            Debug.LogWarning("Addressables had no active profile; activated '"
                             + profiles.GetProfileName(profileId) + "'.");

            return profileId;
        }

        private static void SetGroupPaths(
            AddressableAssetSettings settings, string groupName, string buildPathVariable, string loadPathVariable)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                Debug.LogError("Addressables group '" + groupName + "' was not found.");
                return;
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, buildPathVariable);
            schema.LoadPath.SetVariableByName(settings, loadPathVariable);

            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(schema);
        }

        private static void SetPlayModeBuilder<T>(AddressableAssetSettings settings)
        {
            for (var i = 0; i < settings.DataBuilders.Count; i++)
            {
                if (settings.DataBuilders[i] is T)
                {
                    settings.ActivePlayModeDataBuilderIndex = i;
                    return;
                }
            }

            Debug.LogWarning("Play mode script " + typeof(T).Name + " is not registered; left as it was.");
        }

        private static void Save(AddressableAssetSettings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
