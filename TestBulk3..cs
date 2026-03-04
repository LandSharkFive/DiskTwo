using DiskTwo;
using UnitTestMain;
using UnitTestOne;

namespace UnitTestThree
{
    [TestClass]
    public sealed class UnitThree
    {
        private bool IsDebugStress = TestSettings.CanDebugStress;

        /// <summary>
        /// An insertion test for a small number of items.  Ten items or less.
        /// Test searches. Test one delete. Test one min and one max.
        /// This is a general purpose sanity check for the code.
        /// </summary>
        [TestMethod]
        public void BulkTestOrderTen()
        {
            string myPath = "bacon.db";
            File.Delete(myPath);

            // 1. Generate 100 sorted keys
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 100; i++) data.Add(new Element(i * 10, i + 5));

            // 2. Run Bulk Loader.  Default Capacity (80%).
            var builder = new TreeBuilder(order: 10);
            builder.CreateFromSorted(data, myPath);

            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Verify results using your existing BTree methods
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Check for ghosts
                tree.CheckGhost();

                // 7. Verify keys.
                var list = tree.GetKeys();
                Assert.AreEqual(data.Count, list.Count);
                Assert.IsTrue(Util.IsSorted(list), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(list), "Duplicate found");

                // 8. Zombies
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkTestOrderTwenty()
        {
            string myPath = "apple.db";
            File.Delete(myPath);

            // 1. Generate 100 sorted keys
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 100; i++) data.Add(new Element(i * 10, i * 100));

            // 2. Run Bulk Loader.  Default Capacity (80%).
            var builder = new TreeBuilder(order: 20);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");


                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Search for keys.
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Check for ghosts
                tree.CheckGhost();

                // 7. Verify keys.
                var list = tree.GetKeys();
                Assert.AreEqual(data.Count, list.Count);
                Assert.IsTrue(Util.IsSorted(list), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(list), "Duplicate found");

                // 8. Zombies
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkTestOrderThirtyFullCapacityFive()
        {
            string myPath = "blue.db";
            File.Delete(myPath);

            // 1. Generate 100 sorted keys
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 100; i++) data.Add(new Element(i * 10, i * 10));

            // 2. Run Bulk Loader.  Default Capacity (80%).
            var builder = new TreeBuilder(order: 30);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Verify results using your existing BTree methods
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Verify keys.
                var list = tree.GetKeys();
                Assert.AreEqual(data.Count, list.Count);
                Assert.IsTrue(Util.IsSorted(list), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(list), "Duplicate key found.");

                // 7. Check for zombies.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacitySmallOrderOne()
        {
            string myPath = "toast.db";
            File.Delete(myPath);

            // 1. Generate 16 sorted keys. 
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 16; i++) data.Add(new Element(i, i));

            // 2. Run Bulk Loader.  Full Capacity (80%).
            var builder = new TreeBuilder(order: 4, 1.0);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Search for keys.
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Verify keys.  
                var sortedKeys = tree.GetKeys();
                Assert.IsTrue(Util.IsSorted(sortedKeys), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(sortedKeys), "Duplicate key found.");

                // 7. Check for zombies.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacityMediumOrder()
        {
            string myPath = "bear.db";
            File.Delete(myPath);

            // 1. Generate 100 sorted keys.  
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 100; i++) data.Add(new Element(i, i));

            // 2. Run Bulk Loader.  Full Capacity (80%).
            var builder = new TreeBuilder(order: 10, 1.0);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Search for keys.
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Verify keys.
                var sortedKeys = tree.GetKeys();
                Assert.IsTrue(Util.IsSorted(sortedKeys), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(sortedKeys), "Duplicate key found.");

                // 7. Check for zombies.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacitySmallOrderTwo()
        {
            string myPath = "jam.db";
            File.Delete(myPath);

            // 1. Generate 50 sorted keys.
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 50; i++) data.Add(new Element(i, i));
            List<int> extra = new List<int>();
            for (int i = 51; i <= 60; i++) extra.Add(i);

            // 2. Run Bulk Loader.  Full Capacity (80%).
            var builder = new TreeBuilder(order: 4, 1.0);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Search for keys.
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // Insert keys.
                foreach (int i in extra) tree.Insert(i, i * 10);

                // 7. Search for keys.
                foreach (int i in extra)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Missing Key {i}");
                }

                // 8. Verify keys.
                var sortedKeys = tree.GetKeys();
                Assert.IsTrue(Util.IsSorted(sortedKeys), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(sortedKeys), "Duplicate keys found.");


                // 9. Check for zombies.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacitySmallOrderThree()
        {
            string myPath = "sugar.db";
            File.Delete(myPath);

            // 1. Generate 24 sorted keys. 
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 24; i++) data.Add(new Element(i, i));
            List<int> extra = new List<int>();
            for (int i = 25; i <= 30; i++) extra.Add(i);

            // 2. Run Bulk Loader.  Full Capacity (100%).
            var builder = new TreeBuilder(order: 5, 1.0);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. Verify all keys are present.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Search for keys.
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Check for zombies.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");

                // Insert keys.
                foreach (int i in extra) tree.Insert(i, i * 10);

                // 7. Search for keys.
                foreach (int i in extra)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Missing Key {i}");
                }

                // 8. Check for zombies again.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }

        [TestMethod]
        public void BulkLoadFullCapacityMediumOrderFour()
        {
            string myPath = "beef.db";
            File.Delete(myPath);

            // 1. Generate 80 sorted keys. 
            List<Element> data = new List<Element>();
            for (int i = 1; i <= 80; i++) data.Add(new Element(i, i));
            List<int> extra = new List<int>();
            for (int i = 81; i <= 100; i++) extra.Add(i);

            // 2. Run Bulk Loader.  Full Capacity (100%).
            var builder = new TreeBuilder(order: 20, 1.0);
            builder.CreateFromSorted(data, myPath);


            using (var tree = new BTree(myPath))
            {
                // 3. Check Root.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");

                // 4. All keys must exist.
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Bulk Load Failed.");

                // 5. Search for keys.
                foreach (var item in data)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(item.Key, out pair), $"Missing Key {item.Key}");
                }

                // 6. Check for zombies.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");

                // 7. Insert keys. 
                foreach (int i in extra) tree.Insert(i, i * 10);

                // 8. Count the keys.
                count = tree.CountKeys(tree.Header.RootId);
                int totalKeys = data.Count + extra.Count;
                Assert.AreEqual(totalKeys, count, "Key counts must match.");

                // 9. Search for keys.
                foreach (int i in extra)
                {
                    Element pair;
                    Assert.IsTrue(tree.TrySearch(i, out pair), $"Missing Key {i}");
                }

                // 10. Check the keys.
                var sortedKeys = tree.GetKeys();
                Assert.IsTrue(Util.IsSorted(sortedKeys), "Keys must be sorted.");
                Assert.IsFalse(Util.HasDuplicate(sortedKeys), "Duplicate keys found.");

                // 11. Check for zombies again.
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(myPath);
        }


        [TestMethod]
        [DataRow(5, 0.9, 500)]
        [DataRow(5, 0.8, 1000)]
        [DataRow(5, 0.8, 1000)]
        [DataRow(5, 0.7, 1000)]
        [DataRow(5, 0.6, 1000)]

        public void StressTestDelta(int order, double fill, int totalKeys)
        {
            if (!IsDebugStress)
            {
                Assert.Inconclusive("Skipped");
            }

            string testPath = TestHelper.GetTempDb();   // Path.ChangeExtension(Path.GetRandomFileName(), "db");
            File.Delete(testPath);

            // 1. Setup: Generate sorted keys
            var testKeys = Enumerable.Range(1, totalKeys)
                .Select(i => new Element { Key = i, Data = i * 10 }).ToList();

            var builder = new TreeBuilder(order: order, fill);

            builder.CreateFromSorted(testKeys, testPath);

            using (var tree = new BTree(testPath))
            {

                // 1. Count the keys.
                Assert.IsTrue(tree.Header.RootId != -1, "Root");
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(totalKeys, count, "Key Count must match.");

                // 2. Search every key.
                var allKeys = tree.GetKeys();
                bool pass = allKeys.All(k => tree.TrySearch(k, out var e) && e.Data == k * 10);
                Assert.IsTrue(pass, "Missing Keys");
                Assert.IsTrue(tree.GetHeight() < 10, "Height must be be 10 or less.");

                // 3. Run the high-speed single-pass audit.
                var report = tree.PerformFullAudit();
                Assert.AreEqual(0, report.ZombieCount, "Zombies");
                Assert.AreEqual(0, report.GhostCount, "Ghosts");
                if (tree.Header.NodeCount > 10)
                    Assert.IsTrue(report.AverageDensity > 25.0, "Density must be 25% or more.");
            }
            File.Delete(testPath);
        }


    }
}
