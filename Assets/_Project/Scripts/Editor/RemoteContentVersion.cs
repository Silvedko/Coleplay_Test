using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;

namespace Game.Editor
{
    /// <summary>
    /// A unique folder name for every Addressables content build.
    ///
    /// Bundle file names stay the same between builds when the assets do not change, even though the
    /// bytes may differ (e.g. after an editor upgrade). Served from the same URL, a CDN or the local
    /// bundle cache can then return the old file for the new catalog, and the load fails with a CRC
    /// mismatch. Putting each build's bundles in their own folder makes every URL unique.
    ///
    /// Referenced from the Addressables profile as <c>[Game.Editor.RemoteContentVersion.Current]</c>.
    /// Addressables clears its cache of such values when a build starts, so one build always sees one
    /// value; a fresh one is generated as soon as the build finishes.
    /// </summary>
    [InitializeOnLoad]
    public static class RemoteContentVersion
    {
        public const string ProfileToken = "[Game.Editor.RemoteContentVersion.Current]";

        static RemoteContentVersion()
        {
            Current = NewId();
            BuildScript.buildCompleted += _ => Current = NewId();
        }

        public static string Current { get; private set; }

        private static string NewId()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        }
    }
}
