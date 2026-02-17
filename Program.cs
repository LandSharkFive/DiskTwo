namespace DiskTwo
{
    internal class Program
    {

        /// <summary>
        /// The application entry point and interactive test harness for the B-Tree engine.
        /// </summary>
        /// <remarks>
        /// Provides a console-driven interface to execute specific unit tests, 
        /// structural verifications, and high-volume stress tests.
        /// </remarks>
        public static void Main(string[] args)
        {
            Console.WriteLine("Select a test: ");
            int choice = 0;
            Int32.TryParse(Console.ReadLine(), out choice);
            switch (choice)
            {
                case 1:
                    ShowMenu();
                    break;
                case 2:
                    VerifyBasicOperations();
                    break;
                case 3:
                    VerifySequentialInsert();
                    break;
                case 4:
                    VerifyMixedOperations();
                    break;
                case 5:
                    VerifyBulkLoad();
                    break;
                case 6:
                    VerifyCompact();
                    break;
                case 7:
                    RunSanityCheck();
                    break;
                case 8:
                    TestTopDown();
                    break;
            }
        }

        private static void ShowMenu()
        {
            Console.WriteLine("1: Show Menu");
            Console.WriteLine("2: Basic Operations Test");
            Console.WriteLine("3: Sequential Insert Test");
            Console.WriteLine("4: Mixed Insert/Delete Test");
            Console.WriteLine("5: Bulk Load Test");
            Console.WriteLine("6: Compact Test");
            Console.WriteLine("7: Run Sanity Check");
            Console.WriteLine("8: Test Top Down");
        }


        /// <summary>
        /// Insertion test for a small number of items. 
        /// Test searches. Test one deletion.  Test min and one max.
        /// This is a general sanity check.
        /// </summary>
        private static void VerifyBasicOperations()
        {
            Console.WriteLine("Inserting eight items.");
            string outFileName = "aa.bin";
            File.Delete(outFileName);

            // 1. Create the B-Tree (Order 4, meaning max 3 keys per node)
            // This will create or overwrite the file.
            Console.WriteLine("Creating B-Tree of Order 4 (Max 3 keys/node, Max 4 children/node)");
            using (var tree = new BTree(outFileName, order: 4))
            {

                // 2. Insert elements
                Console.WriteLine("Inserting keys: 10, 20, 30, 40, 50, 60, 70, 80");
                tree.Insert(10, 100);
                tree.Insert(20, 200);
                tree.Insert(30, 300); // Node 0: [10, 20, 30] (Full)

                // Inserting 40 will cause a split: 20 promoted to a new root.
                tree.Insert(40, 400);

                // A B-Tree of order 4 with keys 10, 20, 30, 40 now looks like:
                // Root (Disk 0): [20]
                // Left Child (Disk 1): [10]
                // Right Child (Disk 2): [30, 40]
                // 

                tree.Insert(50, 500);
                tree.Insert(60, 600);
                tree.Insert(70, 700);
                tree.Insert(80, 800);

                int count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");

                // 3. Print the Tree
                Console.WriteLine("Current B-Tree Structure (Level-Order Traversal):");
                tree.PrintTreeByLevel();

                // 3a. Print
                List<int> a = tree.GetKeys();
                Console.Write($"Keys: ");
                Util.PrintList(a);

                // 4. Search for an element
                int searchKey = 50;
                Console.WriteLine($"Searching for key {searchKey}");
                Element item;
                if (tree.TrySearch(searchKey, out item)) Console.WriteLine($"Key {searchKey} found.");
                else Console.WriteLine($"Key {searchKey} missing.");

                // 5. Delete first element
                Console.WriteLine("Deleting key 10.");
                tree.Delete(10, 100);

                Console.WriteLine("B-Tree after deletion of 10:");
                tree.PrintTreeByLevel();

                // 3a. Print
                a = tree.GetKeys();
                Console.Write($"Keys: ");
                Util.PrintList(a);

                // 6. Find Max.
                Element? max = tree.FindMax();
                if (max.HasValue)
                {
                    Console.WriteLine($"Maximum element found: {max}");
                }

                // 7. Find Min.
                Element? min = tree.FindMin();
                if (min.HasValue)
                {
                    Console.WriteLine($"Minimum element found: {min}");
                }

                // 8. Count Zombies.
                Console.WriteLine($"Zombie Count: {tree.CountZombies()}");
            }

            File.Delete(outFileName);
        }


        /// <summary>
        /// A sequential insertion test for testing split nodes.
        /// </summary>
        private static void VerifySequentialInsert()
        {
            Console.WriteLine("Insert fifty items.");
            string outFileName = "bb.bin";
            File.Delete(outFileName);

            // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order: 4))
            {
                List<int> data = new List<int>();
                Console.WriteLine("--- Phase 1: Sequential Insertion ---");
                for (int i = 1; i <= 50; i++) data.Add(i);

                foreach (int i in data) tree.Insert(i, i);

                Console.WriteLine("Inserted 50 items.");
                Console.WriteLine($"RootId: {tree.Header.RootId}");

                int count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");

                Console.WriteLine("\n--- Phase 2: Verification ---");
                bool pass = true;
                foreach (int i in data)
                {
                    Element item;
                    if (!tree.TrySearch(i, out item))
                    {
                        pass = false;
                        Console.WriteLine($"Missing Key {i}");
                    }
                }

                Console.WriteLine(pass ? "Pass" : "Failed");
                Console.WriteLine($"Zombie Count: {tree.CountZombies()}");
            }

            File.Delete(outFileName);
        }


        /// <summary>
        /// A sequential insertion and deletion test.
        /// </summary>
        private static void VerifyMixedOperations()
        {
            Console.WriteLine("Test insert and delete.");
            string outFileName = "cc.bin";
            File.Delete(outFileName);

            // Small order forces lots of splits.
            using (var tree = new DiskTwo.BTree(outFileName, order: 4))
            {
                Console.WriteLine("--- Phase 1: Sequential Insertion ---");
                List<int> data = new List<int>();
                for (int i = 1; i <= 10; i++) data.Add(i);

                foreach (int i in data) tree.Insert(i, i);

                Console.WriteLine("Inserted 10 items.");
                Console.WriteLine($"RootId: {tree.Header.RootId}");
                int count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");

                Console.WriteLine("\n--- Phase 2: Verification ---");
                bool pass = true;
                for (int i = 1; i <= 10; i++)
                {
                    Element item;
                    if (!tree.TrySearch(i, out item))
                    {
                        pass = false;
                        Console.WriteLine($"Key {i} missing");
                    }
                }

                Console.WriteLine(pass ? "Pass" : "Failed");

                Console.WriteLine("--- Phase 3: Sequential Deletion ---");
                tree.Delete(1, 1);

                Console.WriteLine("RootId: " + tree.Header.RootId);

                tree.PrintTreeByLevel();
                tree.DumpFile();
                tree.PrintPointers();
                tree.PrintByRoot();

                Console.WriteLine("--- Phase 4: Post-Deletion Verification ---");
                Console.WriteLine($"Zombie Count: {tree.CountZombies()}");
                Console.WriteLine($"Ghost Count: {tree.CountGhost()}");
                Console.WriteLine($"FreeList Count: {tree.GetFreeListCount()}");
                tree.PrintFreeList();
            }

            File.Delete(outFileName);
        }


        /// <summary>
        /// Test the Bulk Loader.
        /// </summary>
        static void VerifyBulkLoad()
        {
            Console.WriteLine("Bulk Load");
            string myPath = "dd.db";
            File.Delete(myPath);

            // 2. Generate 100 sorted keys.
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 100; i++) data.Add(new Element(i * 10, i));

            // 2. Run Bulk Loader.  Set fill factors to 1.0 (100%). 
            var builder = new TreeBuilder(order: 10, 1.0);
            builder.CreateFromSorted(data, myPath);

            using (var tree = new BTree(myPath))
            {

                // 3. Verify results using your existing BTree methods.
                Console.WriteLine("\n--- Verification ---");
                int count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");
                Console.WriteLine($"Expected Count: {data.Count}");
                var list = tree.GetKeys();
                Console.WriteLine($"Sorted: {Util.IsSorted(list)}");
                Console.WriteLine($"Duplicate: {Util.HasDuplicate(list)}");
                tree.DumpFile();
                tree.PrintPointers();
                Console.WriteLine("--- PRINT BY LEVEL ---");
                tree.PrintTreeByLevel();

                int searchKey = 750;
                Console.WriteLine($"Searching for key {searchKey}");
                Element item;
                if (tree.TrySearch(searchKey, out item)) Console.WriteLine($"Found {searchKey}");
                else Console.WriteLine($"Missing {searchKey}");

                Console.WriteLine("\n--- Post-Deletion Verification ---");
                Console.WriteLine($"Zombie Count: {tree.CountZombies()}");
                Console.WriteLine($"Ghost Count: {tree.CountGhost()}");
                Console.WriteLine($"FreeList Count: {tree.GetFreeListCount()}");
                tree.PrintFreeList();
            }

            File.Delete(myPath);
        }

        /// <summary>
        /// Fill disk, delete items to create fragmentation, and trigger compaction.
        /// </summary>
        private static void VerifyCompact()
        {
            Console.WriteLine("\nCompact");
            string dbPath = "blue.db";
            File.Delete(dbPath);

            using (var tree = new BTree(dbPath, order: 10))
            {
                // 1. Bloat the file.
                List<int> data = new List<int>();
                for (int i = 1; i <= 200; i++) data.Add(i);

                foreach (int i in data) tree.Insert(i, i);

                long initialSize = new FileInfo(dbPath).Length;

                // 2. Mass Delete (creates empty nodes).
                var evenNumbers = data.Where(n => n % 2 == 0).ToList();
                foreach (int i in evenNumbers)
                {
                    tree.Delete(i, i * 10);
                }

                int count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");
                Console.WriteLine($"Expected Count: 100");
                Console.WriteLine($"FreeList Count: {tree.GetFreeListCount()}");
                Console.WriteLine($"Zombie Count: {tree.CountZombies()}");

                // 3. Compact
                tree.Compact();

                long finalSize = new FileInfo(dbPath).Length;
                Console.WriteLine($"File Size: {initialSize} bytes.");
                Console.WriteLine($"Final Size: {finalSize} bytes.");
                Console.WriteLine($"Space Reclaimed: {initialSize - finalSize} bytes.");
                Console.WriteLine($"FreeList Count: {tree.GetFreeListCount()}");
                count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");
                Console.WriteLine($"Expected Count: 100");
            }

            File.Delete(dbPath);
        }


        private static void RunSanityCheck()
        {
            VerifyBasicOperations();
            VerifySequentialInsert();
            VerifyMixedOperations();
            VerifyBulkLoad();
            VerifyCompact();
        }

        private static void TestTopDown()
        {
            int order = 128;
            var scenarios = new[] {
                (Order: order, Fill: 1.0, Keys: 1000),
                (Order: order, Fill: 0.95, Keys: 1000),
                (Order: order, Fill: 0.9, Keys: 1000),
                (Order: order, Fill: 0.8, Keys: 1000),
                (Order: order, Fill: 0.7, Keys: 1000),
                (Order: order, Fill: 0.6, Keys: 1000),
                (Order: order, Fill: 0.5, Keys: 1000)
            };

            foreach (var s in scenarios)
            {
                TestBuildTree(s.Order, s.Fill, s.Keys);
            }
        }

        private static void TestBuildTree(int order, double fill, int totalKeys)
        {
            string testPath = "tango.db";
            File.Delete(testPath);

            // 1. Setup: Generate sorted keys.
            var testKeys = Enumerable.Range(1, totalKeys)
                .Select(i => new Element { Key = i, Data = i * 10 }).ToList();

            Console.WriteLine();
            Console.WriteLine($"--- Testing Order: {order}, Fill: {fill} ---");
            var builder = new TreeBuilder(order: order, fill);

            builder.CreateFromSorted(testKeys, testPath);

            using (var tree = new BTree(testPath))
            {
                Console.WriteLine($"RootId: {tree.Header.RootId}");
                int count = tree.CountKeys(tree.Header.RootId);
                Console.WriteLine($"Key Count: {count}");

                var allKeys = tree.GetKeys();

                bool dataIntegrity = allKeys.All(k => tree.TrySearch(k, out var e) && e.Data == k * 10);
                Console.WriteLine($"Integrity Check: {(dataIntegrity ? "PASSED" : "FAILED")}");

                Console.WriteLine($"HEIGHT: {tree.GetHeight()}");

                // 1. Run the high-speed single-pass audit
                var report = tree.PerformFullAudit();
                Console.WriteLine($"Zombie Count {report.ZombieCount}");
                Console.WriteLine($"Ghost Count: {report.GhostCount}");
                Console.WriteLine($"Density: {report.AverageDensity:F2}%");
            }
            File.Delete(testPath);
        }


    }
}


