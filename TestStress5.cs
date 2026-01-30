using DiskTwo;

namespace UnitTestFive
{
    [TestClass]
    public sealed class UnitFive
    {
        [TestMethod]
        public void StressTestRandomThousand()
        {
            long startMemory = GC.GetTotalAllocatedBytes(true);

            string path = "kilo.db";
            File.Delete(path);

            int order = 10;
            int count = 1000;
            using (var tree = new BTree(path, order))
            {
                var random = new Random();
                var keys = new HashSet<int>();

                // 1. Bulk Insertion of Random Keys
                while (keys.Count < count)
                {
                    int k = random.Next(1, 100000);
                    if (keys.Add(k))
                    {
                        tree.Insert(k, k * 2);
                    }
                }

                // 2. Integrity Check
                tree.CheckGhost(); // Ensure no internal nodes were emptied incorrectly
                var sortedKeys = tree.GetKeys();
                Assert.AreEqual(count, sortedKeys.Count, "Key count mismatch.");
                Assert.IsTrue(Util.IsSorted(sortedKeys), "Not sorted.");

                // 3. Random Search Verification
                foreach (var k in keys.Take(100))
                {
                    Element result;
                    Assert.IsTrue(tree.TrySearch(k, out result), $"Missing key {k}");
                    Assert.AreEqual(k * 2, result.Data);
                }

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            long endMemory = GC.GetTotalAllocatedBytes(true);
            Console.WriteLine($"Total Allocated: {(endMemory - startMemory) / 1024.0} KB");

            File.Delete(path);
        }

        [TestMethod]
        public void CompactTestOne()
        {
            string path = "delta.db";
            File.Delete(path);

            int order = 10;
            int count = 1000;
            int deleteCount = 100;

            using (var tree = new BTree(path, order))
            {
                var random = new Random();
                var keys = new HashSet<int>();

                while (keys.Count < count)
                {
                    int k = random.Next(1, 100000);
                    if (keys.Add(k)) tree.Insert(k, k * 2);
                }

                // Delete a portion to create fragmentation/holes in the file
                var keysToDelete = keys.Take(deleteCount).ToList();
                foreach (var k in keysToDelete)
                {
                    tree.Delete(k, k * 2);
                    keys.Remove(k);
                }

                tree.ValidateIntegrity();

                long sizeBefore = new FileInfo(path).Length;

                // EXECUTE COMPACT
                tree.Compact();

                long sizeAfter = new FileInfo(path).Length;

                // 1. PHYSICAL ASSERT: File must be smaller.
                Assert.IsTrue(sizeAfter < sizeBefore, $"Compaction failed. Before: {sizeBefore}, After: {sizeAfter}");

                // 2. INTEGRITY ASSERT: Root must be valid.
                Assert.IsFalse(tree.Header.RootId < 0, "Root lost");

                // 3. DATA ASSERT: Every remaining key must still be searchable and correct.
                foreach (var k in keys)
                {
                    Element result;
                    Assert.IsTrue(tree.TrySearch(k, out result), $"Key {k} missing");
                    Assert.AreEqual(k * 2, result.Data, "Corrupted");
                }

                // 4. STRUCTURE ASSERT: Ensure the B-Tree logic still holds
                tree.CheckGhost();
                Assert.AreEqual(keys.Count, tree.GetKeys().Count, "Missing Keys");

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void IntegrityCheckOne()
        {
            string path = "carrot.db";
            File.Delete(path);

            int order = 10;

            using (var tree = new BTree(path, order))
            {
                // 1. Build a specific structure: Root with two children
                // Inserting 10, 20, 30. With Order 4, this may stay in one node.
                // We insert more to force a split.
                int[] keys = { 10, 20, 30, 40, 50, 60 };
                foreach (var k in keys) tree.Insert(k, k * 100);

                // 2. Perform a deletion that triggers MergeChildren
                // We delete keys until a node hits t-1 and its sibling is also thin.
                tree.Delete(60, 6000);
                tree.Delete(50, 5000);

                // 3. Run Integrity Check prior to compaction
                // This ensures Case 3 didn't leave orphaned IDs in Kids[]
                tree.ValidateIntegrity();

                // 4. Verify physical space management
                // Ensure deleted node IDs were pushed to FreeList.
                tree.Compact();
                Assert.IsTrue(tree.Header.NodeCount < 5, "Compaction failed.");

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void MergeTestOne()
        {
            string path = "pickle.db";
            File.Delete(path);

            using (var tree = new BTree(path, 4))
            {
                List<int> keys = new List<int>();
                for (int i = 1; i <= 20; i++)
                {
                    keys.Add(i * 10);
                }

                foreach (var k in keys) tree.Insert(k, k);

                // 2. Delete to trigger a merge that propagates upward.
                // Deleting 90 should thin the right side, 80 continues the collapse.
                tree.Delete(90, 90);
                tree.Delete(80, 80);
                tree.Delete(70, 70);

                // 3. Verify the root wasn't orphaned and height updated.
                tree.CheckGhost();
                Element item;
                Assert.IsTrue(tree.TrySearch(10, out item));
                Assert.IsTrue(tree.TrySearch(20, out item));
                Assert.IsTrue(tree.TrySearch(30, out item));

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void BorrowFromInternalNodeOne()
        {
            string path = "skunk.db";
            File.Delete(path);

            using (var tree = new BTree(path, 4))
            {
                // Build a balanced tree
                for (int i = 1; i <= 15; i++) tree.Insert(i, i);

                // Delete keys to force an internal node to borrow from a sibling
                // This ensures Kids[0] of the sibling correctly becomes Kids[N] of the borrower
                tree.Delete(1, 1);
                tree.Delete(2, 2);

                // Ensure integrity check passes (no orphaned keys).
                var keys = tree.GetKeys();
                Assert.IsTrue(Util.IsSorted(keys));
                Assert.IsFalse(Util.HasDuplicate(keys));

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void OddEvenTestOne()
        {
            string path = "pear.db";
            File.Delete(path);

            using (var tree = new BTree(path, 4))
            {
                var range = Enumerable.Range(1, 100).ToList();
                foreach (var i in range) tree.Insert(i, i);

                // Delete even numbers
                foreach (var i in range.Where(n => n % 2 == 0))
                {
                    tree.Delete(i, i);
                    Element item;
                    Assert.IsFalse(tree.TrySearch(i, out item), $"Deleted key {i} found.");
                }

                // Verify odd numbers still exist
                foreach (var i in range.Where(n => n % 2 != 0))
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Valid key {i} missing.");
                }

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void HardTestOne()
        {
            string path = "hard.db";
            File.Delete(path);

            using (var tree = new BTree(path, 10))
            {
                Random rng = new Random();
                List<int> tracker = new List<int>();

                // Phase 1: Heavy Churn
                for (int i = 0; i < 2000; i++)
                {
                    int val = rng.Next(1, 10000);

                    // 70% chance to insert, 30% chance to delete
                    if (rng.NextDouble() < 0.7)
                    {
                        if (!tracker.Contains(val))
                        {
                            tree.Insert(val, val);
                            tracker.Add(val);
                        }
                    }
                    else if (tracker.Count > 0)
                    {
                        int toDelete = tracker[rng.Next(tracker.Count)];
                        tree.Delete(toDelete, toDelete);
                        tracker.Remove(toDelete);
                    }

                    // Every 100 ops, verify integrity
                    if (i % 100 == 0)
                    {
                        tree.CheckGhost();
                        var currentKeys = tree.GetKeys();
                        Assert.AreEqual(tracker.Count, currentKeys.Count, $"Count mismatch at op {i}");
                    }
                }

                // Phase 2: Total Liquidation
                foreach (var remaining in tracker.ToList())
                {
                    tree.Delete(remaining, remaining);
                }

                Assert.AreEqual(0, tree.GetKeys().Count, "Tree should be empty");
                int rootId = tree.Header.RootId;
                Assert.IsTrue(rootId == -1 || tree.DiskRead(rootId).NumKeys == 0);

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void CompactPreservesData()
        {
            string path = "sam.db";
            File.Delete(path);

            int order = 10;
            int count = 1000;
            int deleteCount = 100;

            var rnd = new Random();
            var keys = new HashSet<int>();

            using (var tree = new BTree(path, order))
            {
                while (keys.Count < count)
                {
                    int k = rnd.Next(1, 1000000);
                    if (keys.Add(k)) tree.Insert(k, k * 2);
                }

                // Delete some keys
                var toDelete = keys.Take(deleteCount).ToList();
                foreach (var k in toDelete)
                {
                    tree.Delete(k, k * 2);
                    keys.Remove(k);
                }

                tree.ValidateIntegrity();
                long before = new FileInfo(path).Length;

                tree.Compact();

                long after = new FileInfo(path).Length;

                // File should shrink and data preserved
                Assert.IsTrue(after <= before, $"Compact did not shrink file: before={before} after={after}");

                // All remaining keys must still be searchable
                foreach (var k in keys)
                {
                    Element e;
                    Assert.IsTrue(tree.TrySearch(k, out e), $"Missing key after compact: {k}");
                    Assert.AreEqual(k * 2, e.Data);
                }

                tree.ValidateIntegrity();
                var zombies = tree.GetZombies();
                Assert.AreEqual(0, zombies.Count, "Zombies present after compact");
            }

            File.Delete(path);
        }


    }
}
