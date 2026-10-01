using UdonSharp;
using UnityEngine;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchColor005 : PersistenceProbe
    {
        [UdonSynced] public Color V001;
        [UdonSynced] public Color V002;
        [UdonSynced] public Color V003;
        [UdonSynced] public Color V004;
        [UdonSynced] public Color V005;
    }
}
