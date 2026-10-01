using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector4001 : PersistenceProbe
    {
        [UdonSynced] public Vector4 V001 = new Vector4();
    }
}
