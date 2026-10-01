using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchInt001 : PersistenceProbe
    {
        [UdonSynced] public int V001;
    }
}
