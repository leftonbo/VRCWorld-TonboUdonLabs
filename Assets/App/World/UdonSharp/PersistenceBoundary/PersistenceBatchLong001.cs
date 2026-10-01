using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLong001 : PersistenceProbe
    {
        [UdonSynced] public long V001;
    }
}
