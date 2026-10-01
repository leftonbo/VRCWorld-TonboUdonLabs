using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchChar001 : PersistenceProbe
    {
        [UdonSynced] public char V001 = '\0';
    }
}
