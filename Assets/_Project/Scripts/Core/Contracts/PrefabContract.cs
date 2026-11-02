using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Fail-fast lookup for components a prefab is required to carry on its root. Only the root is
    /// checked: a prefab whose component sits elsewhere is a broken prefab, not something to search for.
    /// </summary>
    public static class PrefabContract
    {
        public static T RequireOnRoot<T>(GameObject instance) where T : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!instance.TryGetComponent(out T component))
            {
                throw new InvalidOperationException(
                    "Prefab '" + instance.name + "' must have " + typeof(T).Name + " on its root object.");
            }

            return component;
        }
    }
}
