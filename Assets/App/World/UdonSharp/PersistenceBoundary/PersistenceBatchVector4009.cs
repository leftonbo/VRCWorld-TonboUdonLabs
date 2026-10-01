using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector4009 : PersistenceProbe
    {
        [UdonSynced] public Vector4 V001 = new Vector4();
        [UdonSynced] public Vector4 V002 = new Vector4();
        [UdonSynced] public Vector4 V003 = new Vector4();
        [UdonSynced] public Vector4 V004 = new Vector4();
        [UdonSynced] public Vector4 V005 = new Vector4();
        [UdonSynced] public Vector4 V006 = new Vector4();
        [UdonSynced] public Vector4 V007 = new Vector4();
        [UdonSynced] public Vector4 V008 = new Vector4();
        [UdonSynced] public Vector4 V009 = new Vector4();
    }
}
