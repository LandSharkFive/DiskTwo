using System.Diagnostics;

namespace DiskTwo
{
    public static class Util
    {
        private static readonly Random rnd = new Random();

        /// <summary>
        /// Shuffle a list.
        /// </summary>
        public static void Shuffle(List<int> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rnd.Next(n + 1);
                int value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Print a list.
        /// </summary>
        public static void PrintList(List<int> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Console.Write($"{list[i]} ");
                if (i > 0 && i % 10 == 0)
                {
                    Console.WriteLine();
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Is list sorted?
        /// </summary>
        public static bool IsSorted(List<int> a)
        {
            for (int i = 1; i < a.Count; i++)
            {
                if (a[i - 1] > a[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Does list have any duplicates?
        /// </summary>
        public static bool HasDuplicate(List<int> source)
        {
            var set = new HashSet<int>();
            foreach (var item in source)
            {
                if (!set.Add(item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get memory in megabytes.
        /// </summary>
        public static int GetMemory()
        {
            Process currentProcess = Process.GetCurrentProcess();
            long workingSet = currentProcess.PeakWorkingSet64 / (1024 * 1024);
            return Convert.ToInt32(workingSet);
        }

        /// <summary>
        /// Print the zombie list.
        /// </summary>
        public static void PrintZombies(List<int> list)
        {
            if (list.Count == 0)
            {
                Console.WriteLine("No Zombies");
            }
            else
            {
                Console.WriteLine("Zombie Count: " + list.Count);
                Console.WriteLine("Zombies: " + string.Join(", ", list));
            }
        }


    }
}
