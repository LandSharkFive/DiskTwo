using System.Diagnostics;
using System.Globalization;

namespace DiskTwo
{
    internal class Program
    {
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
                    RunAllTests();
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
            Console.WriteLine("7: Run All Tests");
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
            using (var tree = new BTree(outFileName, 4))
            {

                // 2. Insert elements
                Console.WriteLine("Inserting keys: 10, 20, 30, 40, 50, 60, 70, 80...");
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

                // 3. Print the Tree
                Console.WriteLine("Current B-Tree Structure (Level-Order Traversal):");
                tree.PrintTreeByLevel();
                Console.WriteLine();

                // 3a. Print
                List<int> a = tree.GetKeys();
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
                Console.WriteLine("\n");

                // 3a. Print
                a = tree.GetKeys();
                Util.PrintList(a);


                // 6. Find Min/Max
                Element? max = tree.FindMax();
                if (max.HasValue)
                {
                    Console.WriteLine($"Maximum element found: Key={max.Value.Key}, Data={max.Value.Data}");
                }

                Element? min = tree.FindMin();
                if (min.HasValue)
                {
                    Console.WriteLine($"Minimum element found: Key={min.Value.Key}, Data={min.Value.Data}");
                }

                var list = tree.GetZombies();
                Util.PrintZombies(list);
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

            int order = 4; // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order))
            {
                Console.WriteLine("--- Phase 1: Sequential Insertion ---");
                for (int i = 1; i <= 50; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }
                Console.WriteLine("Inserted 50 items. Current Root Position: " + tree.Header.RootId);

                Console.WriteLine("\n--- Phase 2: Verification ---");
                bool pass = true;
                for (int i = 1; i <= 50; i++)
                {
                    Element item;
                    if (!tree.TrySearch(i, out item))
                    {
                        pass = false;
                        Console.WriteLine($"Key {i} missing");
                    }
                }

                if (pass) Console.WriteLine("Pass");
                else Console.WriteLine("Failed");

                var list = tree.GetZombies();
                Util.PrintZombies(list);
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

            int order = 4; // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order))
            {
                Console.WriteLine("--- Phase 1: Sequential Insertion ---");
                for (int i = 1; i <= 10; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }
                Console.WriteLine("Inserted 10 items. Current Root Position: " + tree.Header.RootId);

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

                if (pass) Console.WriteLine("Pass");
                else Console.WriteLine("Failed");

                Console.WriteLine("--- Phase 3: Sequential Deletion ---");
                tree.Delete(1, 10);

                Console.WriteLine("RootId: " + tree.Header.RootId);

                tree.PrintTreeByLevel();
                tree.PrintPointers();
                tree.PrintByRoot();

                var list = tree.GetZombies();
                Util.PrintZombies(list);
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
            int order = 10; // Small order makes it easier to see splits

            // 1. Initialize a clean BTree file
            File.Delete(myPath);
            using (var tree = new BTree(myPath, order))
            {
                // 2. Generate 100 sorted keys
                List<int> data = new List<int>();
                for (int i = 1; i <= 100; i++) data.Add(i * 10);

                // 3. Run Bulk Loader
                Console.WriteLine("Starting Bulk Load...");
                BulkLoader loader = new BulkLoader(tree, order);
                loader.BulkLoad(data);

                // 4. Verify results using your existing BTree methods
                Console.WriteLine("\n--- Verification ---");
                tree.DumpFile();
                tree.PrintPointers();
                Console.WriteLine("--- PRINT BY LEVEL ---");
                tree.PrintTreeByLevel();

                int searchKey = 750;
                Console.WriteLine($"Searching for key {searchKey}");
                Element item;
                if (tree.TrySearch(searchKey, out item)) Console.WriteLine($"Found {searchKey}");
                else Console.WriteLine($"Missing {searchKey}");

                var list = tree.GetZombies();
                Util.PrintZombies(list);
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

            using (var tree = new BTree(dbPath, 10))
            {
                // 1. Bloat the file
                for (int i = 1; i <= 200; i++) tree.Insert(i, i * 10);
                long initialSize = new FileInfo(dbPath).Length;

                // 2. Mass Delete (creates actual empty nodes)
                for (int i = 1; i <= 150; i++) tree.Delete(i, i * 10);

                Console.WriteLine($"FreeList Count: {tree.GetFreeListCount()}");

                var zombies = tree.GetZombies();
                Console.WriteLine($"Zombie Count: {zombies.Count}");

                // 3. Compact
                tree.Compact();

                long finalSize = new FileInfo(dbPath).Length;
                Console.WriteLine($"File Size: {initialSize} bytes.");
                Console.WriteLine($"Final Size: {finalSize} bytes.");
                Console.WriteLine($"Space Reclaimed: {initialSize - finalSize} bytes.");
                Console.WriteLine($"FreeList Count: {tree.GetFreeListCount()}");
            }

            // Clean up
            File.Delete(dbPath); 
        }


        private static void RunAllTests()
        {
            VerifyBasicOperations();
            VerifySequentialInsert();
            VerifyMixedOperations();
            VerifyBulkLoad();
            VerifyCompact();
        }


    }
}
