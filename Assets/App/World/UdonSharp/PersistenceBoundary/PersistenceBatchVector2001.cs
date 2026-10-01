using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector2001 : PersistenceProbe
    {
        [UdonSynced] public Vector2 V001 = new Vector2();
    }
}
