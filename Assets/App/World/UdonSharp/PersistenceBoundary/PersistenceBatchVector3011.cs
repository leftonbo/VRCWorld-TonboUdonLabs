using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector3011 : PersistenceProbe
    {
        [UdonSynced] public Vector3 V001 = new Vector3();
        [UdonSynced] public Vector3 V002 = new Vector3();
        [UdonSynced] public Vector3 V003 = new Vector3();
        [UdonSynced] public Vector3 V004 = new Vector3();
        [UdonSynced] public Vector3 V005 = new Vector3();
        [UdonSynced] public Vector3 V006 = new Vector3();
        [UdonSynced] public Vector3 V007 = new Vector3();
        [UdonSynced] public Vector3 V008 = new Vector3();
        [UdonSynced] public Vector3 V009 = new Vector3();
        [UdonSynced] public Vector3 V010 = new Vector3();
        [UdonSynced] public Vector3 V011 = new Vector3();
    }
}
