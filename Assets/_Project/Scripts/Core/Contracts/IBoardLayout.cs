using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Decides where each object of the board is placed.</summary>
    public interface IBoardLayout
    {
        /// <summary>Returns exactly <paramref name="count"/> world-space poses.</summary>
        IReadOnlyList<Pose> Arrange(int count);
    }
}
