using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector3008 : PersistenceProbe
    {
        [UdonSynced] public Vector3 V001 = new Vector3();
        [UdonSynced] public Vector3 V002 = new Vector3();
        [UdonSynced] public Vector3 V003 = new Vector3();
        [UdonSynced] public Vector3 V004 = new Vector3();
        [UdonSynced] public Vector3 V005 = new Vector3();
        [UdonSynced] public Vector3 V006 = new Vector3();
        [UdonSynced] public Vector3 V007 = new Vector3();
        [UdonSynced] public Vector3 V008 = new Vector3();
    }
}
