using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The board the player searches: a fixed set of objects that all wear the same texture, one of
    /// which is the hidden target. Looking alike is the point, so the board dresses every object at
    /// once and never marks the target in any way - the only clue is where it was found last.
    ///
    /// The board is handed its objects already spawned and keeps them for the whole session; a round
    /// changes what they wear and which one is hiding, never how many there are.
    /// </summary>
    public sealed class TargetBoard
    {
        private readonly IReadOnlyList<ITargetView> _targets;
        private readonly System.Random _random = new System.Random();

        private int _targetIndex = -1;

        public TargetBoard(IReadOnlyList<ITargetView> targets)
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

            // Seeded here rather than by the caller: a board without a hidden target is not a state
            // the rest of the game should ever have to guard against.
            MoveTarget();
        }

        public bool IsTarget(ITargetView view)
        {
            return ReferenceEquals(view, _targets[_targetIndex]);
        }

        /// <summary>Puts one texture on every object, the hidden target included.</summary>
        public void Dress(Texture2D texture)
        {
            for (var i = 0; i < _targets.Count; i++)
            {
                _targets[i].SetTexture(texture);
            }
        }

        /// <summary>Uniform pick among every object except the one hiding the target right now.</summary>
        public void MoveTarget()
        {
            int index;
            if (_targets.Count > 1 && _targetIndex >= 0)
            {
                index = _random.Next(_targets.Count - 1);
                if (index >= _targetIndex)
                {
                    index++;
                }
            }
            else
            {
                index = _random.Next(_targets.Count);
            }

            _targetIndex = index;
        }
    }
}
