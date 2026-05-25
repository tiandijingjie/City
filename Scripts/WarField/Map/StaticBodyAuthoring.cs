using UnityEngine;
using Unity.Mathematics;

namespace WarField
{
    using WE = WarFieldElements;

    public class StaticBodyAuthoring : BodyAuthoring
    {
#region public parameters

#endregion

#region private parameters

        [Header("Pathfinding")]
        [SerializeField] private bool _writeToFlowField = true;  //是否将单体障碍物加入流场, 如果加入流场会导致整个cell无法通行,所以如果障碍物体积小就不要加入流场,而是在士兵行走的过程中通过排斥力来远离

#endregion

#region private parameters' get set

        public override bool gs_writeToFlowField
        {
            get { return _writeToFlowField; }
        }
#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

#endregion
    }
}
