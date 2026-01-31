using DiskTwo;

namespace UnitTestThree
{
    [TestClass]
    public sealed class UnitThree
    {
        /// <summary>
        /// An insertion test for a small number of items.  Ten items or less.
        /// Test seiches. Test one deletion.  Test one min and one max.
        /// Ten items or less.  This is a general purpose sanity check for the code.
        /// </summary>
        [TestMethod]
        public void BulkTestOrderTen()
        {
            string myPath = "bacon.db";
            int order = 10; // Small order makes it easier to see splits

            // 1. Initialize a clean BTree file
            File.Delete(myPath);
            using (var tree = new BTree(myPath, order))
            {

                // 2. Generate 100 sorted keys
                List<int> data = new List<int>();
                for (int i = 1; i <= 100; i++) data.Add(i * 10);

                // 3. Run Bulk Loader
                BulkLoader loader = new BulkLoader(tree, order);
                loader.BulkLoad(data);

                // 4. Verify results using your existing BTree methods
                for (int i = 10; i <= 100; i += 10)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Key missing {i}");
                }

                tree.CheckGhost();

                int searchKey = 750;
                Element item;
                bool found = tree.TrySearch(searchKey, out item);
                Assert.IsTrue(found);

                // 5. Verify keys.
                var list = tree.GetKeys();
                Assert.AreEqual(100, list.Count);
                Assert.IsTrue(Util.IsSorted(list), "Not Sorted");
                Assert.IsFalse(Util.HasDuplicate(list), "Duplicate");

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkTestOrderTwenty()
        {
            string myPath = "apple.db";
            int order = 20; 

            // 1. Initialize a clean BTree file
            File.Delete(myPath);
            using (var tree = new BTree(myPath, order))
            {
                // 2. Generate 100 sorted keys
                List<int> data = new List<int>();
                for (int i = 1; i <= 100; i++) data.Add(i * 10);

                // 3. Run Bulk Loader
                BulkLoader loader = new BulkLoader(tree, order);
                loader.BulkLoad(data);

                // 4. Verify results using your existing BTree methods
                for (int i = 10; i <= 100; i += 10)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Key missing {i}");
                }

                tree.CheckGhost();

                int searchKey = 500;
                Element item;
                bool found = tree.TrySearch(searchKey, out item);
                Assert.IsTrue(found);

                // 5. Verify keys.
                var list = tree.GetKeys();
                Assert.AreEqual(100, list.Count);
                Assert.IsTrue(Util.IsSorted(list), "Not Sorted");
                Assert.IsFalse(Util.HasDuplicate(list), "Duplicate");

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkTestOrderThirty()
        {
            string myPath = "blue.db";
            int order = 30; 

            // 1. Initialize a clean BTree file
            File.Delete(myPath);
            using (var tree = new BTree(myPath, order))
            {
                // 2. Generate 100 sorted keys
                List<int> data = new List<int>();
                for (int i = 1; i <= 100; i++) data.Add(i * 10);

                // 3. Run Bulk Loader
                BulkLoader loader = new BulkLoader(tree, order, 1.0, 1.0);
                loader.BulkLoad(data);

                // 4. Verify results using your existing BTree methods
                for (int i = 10; i <= 100; i += 10)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Key missing {i}");
                }

                int searchKey = 500;
                Element item;
                bool found = tree.TrySearch(searchKey, out item);
                Assert.IsTrue(found);

                // 5. Verify keys.
                var list = tree.GetKeys();
                Assert.AreEqual(100, list.Count);
                Assert.IsTrue(Util.IsSorted(list), "Not Sorted");
                Assert.IsFalse(Util.HasDuplicate(list), "Duplicate");

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacitySmallOrderOne()
        {
            string myPath = "toast.db";
            int order = 4;

            File.Delete(myPath);
            using (var tree = new BTree(myPath, order))
            {

                // 1. Generate 16 sorted keys. 15 works too.
                List<int> data = new List<int>();
                for (int i = 1; i <= 16; i++) data.Add(i);

                // 2. Run Bulk Loader.  Set fill factors to 1.0 (100%).
                BulkLoader loader = new BulkLoader(tree, order, 1.0, 1.0);
                loader.BulkLoad(data);

                // 3. Search for keys.
                foreach (int i in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Key missing {i}");
                }

                // 4. Check for zombies.
                var list = tree.GetZombies();
                Console.WriteLine($"Zombies: {list.Count}");
                Assert.AreEqual(0, list.Count, "Zombies found");
                Console.WriteLine($"Free List: {tree.GetFreeListCount()}");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacityMediumOrder()
        {
            string myPath = "bear.db";
            int order = 10;

            File.Delete(myPath);
            using (var tree = new BTree(myPath, order))
            {

                // 1. Generate 100 sorted keys.  99 works too.
                List<int> data = new List<int>();
                for (int i = 1; i <= 100; i++) data.Add(i);

                // 2. Run Bulk Loader.  Set fill factors to 1.0 (100%).
                BulkLoader loader = new BulkLoader(tree, order, 1.0, 1.0);
                loader.BulkLoad(data);

                // 3. Search for keys.
                foreach (int i in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Key missing {i}");
                }

                // 4. Check for zombies.
                var list = tree.GetZombies();
                Console.WriteLine($"Zombies: {list.Count}");
                Assert.AreEqual(0, list.Count, "Zombies found");
                Console.WriteLine($"Free List: {tree.GetFreeListCount()}");
            }

            File.Delete(myPath);
        }



    }
}
