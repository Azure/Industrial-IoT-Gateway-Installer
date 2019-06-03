using System.Threading;

namespace IoTEdgeInstaller
{
    public class ThreadPoolHelper
    {
        public static bool QueueUserWorkItem(WaitCallback callBack, object state)
        {
            void safeCallback(object o)
            {
               callBack(o);
            }

            return ThreadPool.QueueUserWorkItem(safeCallback, state);
        }
    }
}