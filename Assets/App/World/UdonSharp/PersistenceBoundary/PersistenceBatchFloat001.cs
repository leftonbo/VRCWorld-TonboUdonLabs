using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchFloat001 : PersistenceProbe
    {
        [UdonSynced] public float V001;
    }
}
