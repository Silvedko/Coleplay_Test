using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The objects the player searches. All of them wear the same texture, so nothing marks the
    /// hidden target. The set of objects is fixed for the whole session.
    /// </summary>
    public sealed class TargetBoard : ITargetBoard
    {
        private readonly IReadOnlyList<ITargetView> _targets;
        private readonly System.Random _random;

        private int _targetIndex = -1;

        public TargetBoard(IReadOnlyList<ITargetView> targets, System.Random random)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (targets.Count == 0)
            {
                throw new ArgumentException("The board needs at least one object.", nameof(targets));
            }

            _targets = targets;
            _random = random ?? throw new ArgumentNullException(nameof(random));

            // A board always has a hidden target, so callers never have to check for "none".
            MoveTarget();
        }

        public bool IsTarget(ITargetView view)
        {
            return ReferenceEquals(view, _targets[_targetIndex]);
        }

        public void Dress(Texture2D texture)
        {
            for (var i = 0; i < _targets.Count; i++)
            {
                _targets[i].SetTexture(texture);
            }
        }

        /// <summary>Uniform pick among every object except the current target.</summary>
        public void MoveTarget()
        {
            if (_targets.Count == 1 || _targetIndex < 0)
            {
                _targetIndex = _random.Next(_targets.Count);
                return;
            }

            var index = _random.Next(_targets.Count - 1);
            _targetIndex = index >= _targetIndex ? index + 1 : index;
        }
    }
}
