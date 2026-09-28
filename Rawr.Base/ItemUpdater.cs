using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

namespace Rawr
{
    public class ItemUpdater
    {
        internal class ItemToUpdate
        {
            public Item item;
            public int index;
        }

        private int itemsDone;
        private int itemsToDo;
        private bool finishedInput;
        private bool done;

        public int ItemsDone { get { lock (lockObject) { return itemsDone; } } }
        public int ItemsToDo { get { lock (lockObject) { return itemsToDo; } } }
        public bool Done { get { lock (lockObject) { return done || (cancel != null && cancel()); } } }
        public Item this[int id]
        {
            get
            {
                lock (lockObject)
                {
                    if (id > 0)
                    {
                        Item item; 
                        newItems.TryGetValue(id, out item); 
                        return item;
                    }

                    return null;
                }
            }
        }

        private SortedDictionary<int, Item> newItems;

        private bool useArmory;
        private bool usePTR;
        private bool multiThreaded;
    	private Wowhead.UpgradeCancelCheck cancel;

        private AutoResetEvent eventDone;

        private Object lockObject; // because lock(this) is discouraged by documentation

        private Queue<ItemToUpdate> itemQueue;
        private int itemsPerSecond;
        private Stopwatch rateWatch;
        private long nextDispatchAllowed;

        public ItemUpdater(bool multiThreaded, bool useArmory, bool usePTR, int itemsPerSecond, Wowhead.UpgradeCancelCheck check )
        {
            this.itemsDone = 0;
            this.itemsToDo = 0;
            this.finishedInput = false;
            this.done = false;
            this.newItems = new SortedDictionary<int, Item>();
            this.multiThreaded = multiThreaded;
			this.cancel = check;
            this.useArmory = useArmory;
            this.usePTR = usePTR;
            this.eventDone = new AutoResetEvent(false);
            this.lockObject = new Object();
            this.itemsPerSecond = itemsPerSecond;
            this.itemQueue = new Queue<ItemToUpdate>();
            this.rateWatch = new Stopwatch();
            this.rateWatch.Start();

            if (multiThreaded)
            {
                // Use a small fixed set of dedicated background threads instead of queueing every item
                // onto the .NET thread pool. Queueing thousands of items makes the pool grow to its
                // maximum thread count and it then stops completing the update (observed deadlock).
                int workerCount = Math.Min(Math.Max(itemsPerSecond, 1), 5);
                for (int i = 0; i < workerCount; i++)
                {
                    Thread worker = new Thread(new ThreadStart(WorkerLoop));
                    worker.IsBackground = true;
                    worker.Name = "ItemUpdater worker " + i;
                    worker.Start();
                }
            }
        }

        private void WorkerLoop()
        {
            while (!Done)
            {
                ItemToUpdate info = null;
                lock (lockObject)
                {
                    while (itemQueue.Count == 0 && !finishedInput)
                    {
                        if (Done) return;
                        Monitor.Wait(lockObject, 500);
                    }
                    if (Done) return;
                    if (itemQueue.Count == 0) return; // input finished and nothing left to do
                    info = itemQueue.Dequeue();
                }
                WaitForRateLimit();
                LoadItem(info);
            }
        }

        private void WaitForRateLimit()
        {
            if (itemsPerSecond <= 0) return;
            long wait;
            lock (lockObject)
            {
                long now = rateWatch.ElapsedMilliseconds;
                long interval = 1000 / Math.Max(itemsPerSecond, 1);
                long next = nextDispatchAllowed > now ? nextDispatchAllowed : now;
                wait = next - now;
                nextDispatchAllowed = next + interval;
            }
            if (wait > 0) Thread.Sleep((int)wait);
        }

        private void LoadItem(object state)
        {
            ItemToUpdate info = state as ItemToUpdate;
            Item i = null;

            try
            {
                if (useArmory)
                {
                    i = Item.LoadFromId(info.item.Id, true, false, false);
                }
                else
                {
                    i = Item.LoadFromId(info.item.Id, true, false, true, Rawr.Properties.GeneralSettings.Default.Locale, usePTR ? "wotlk" : "wotlk");
                }
            }
            catch (Exception ex)
            {
                StatusMessaging.ReportError("Load item", ex, string.Format("Unable to update '{0}' due to an error: {1}\r\n\r\n{2}", info.item.Name, ex.Message, ex.StackTrace));
            }

            bool completelyDone = false;

            lock (lockObject)
            {
                // indexer instead of Add, so a duplicate index can never kill a worker and leave
                // the update waiting forever for an item that will never be counted
                if (i != null) newItems[info.index] = i;
                itemsDone++;
                completelyDone = done = finishedInput && itemsDone == itemsToDo;
                Monitor.PulseAll(lockObject);
            }

            if (completelyDone) eventDone.Set();
        }

        public void AddItem(int index, Item item)
        {
            if (!multiThreaded)
            {
                Item i = null;
                try
                {
                    if (useArmory)
                    {
                        i = Item.LoadFromId(item.Id, true, false, false);
                    }
                    else
                    {
                        i = Item.LoadFromId(item.Id, true, false, true, Rawr.Properties.GeneralSettings.Default.Locale, usePTR ? "wotlk" : "wotlk");
                    }
                }
                catch (Exception ex)
                {
                    StatusMessaging.ReportError("Load item", ex, string.Format("Unable to update '{0}' due to an error: {1}\r\n\r\n{2}", item.Name, ex.Message, ex.StackTrace));
                }
                if (i != null)
                {
                    newItems[index] = i;
                }
                return;
            }

            ItemToUpdate info = new ItemToUpdate()
            {
                index = index,
                item = item
            };
            lock (lockObject)
            {
                itemsToDo++;
                itemQueue.Enqueue(info);
                Monitor.Pulse(lockObject);
            }
        }

        public void FinishAdding()
        {
            if (!multiThreaded)
            {
                done = true;
                return;
            }
            lock (lockObject)
            {
                finishedInput = true;
                if (itemsToDo == 0) done = true;
                Monitor.PulseAll(lockObject);
            }
            if (done) eventDone.Set();
        }

        public void WaitUntilDone()
        {
            if (multiThreaded) eventDone.WaitOne();
        }
    }
}
