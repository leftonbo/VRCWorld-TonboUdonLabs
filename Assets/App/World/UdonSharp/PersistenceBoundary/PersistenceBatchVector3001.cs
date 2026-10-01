using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector3001 : PersistenceProbe
    {
        [UdonSynced] public Vector3 V001 = new Vector3();
    }
}
