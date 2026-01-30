using DiskTwo;

namespace UnitTestFour
{
    [TestClass]
    public sealed class UnitFour
    {

        [TestMethod]
        public void TestRoundTripOne()
        {
            string path = "golf.db";
            File.Delete(path);

            int nodeCount;

            // Session 1: Create, Insert, and Delete to populate FreeList
            int order = 4;
            using (var t1 = new BTree(path, order))
            {
                for (int i = 1; i <= 10; i++) t1.Insert(i, i * 100);

                // Deleting items to ensure nodes are moved to FreeList
                t1.Delete(1, 100);
                t1.Delete(2, 200);
                nodeCount = t1.Header.NodeCount;
            }

            // Session 2: Reopen and verify metadata
            using (var t2 = new BTree(path, order))
            {
                // Verify Header integrity
                Assert.AreEqual(order, t2.Header.Order);

                // Verify Data integrity
                Element result;
                Assert.IsFalse(t2.TrySearch(1, out result), "Key 1 should not exist.");
                Assert.IsTrue(t2.TrySearch(10, out result), "Key 10 should exist.");
                Assert.AreEqual(1000, result.Data);

                // Verify FreeList was loaded.
                // If we insert now, it should ideally reuse a deleted ID.
                t2.Insert(11, 1100);
                Assert.AreEqual(nodeCount, t2.Header.NodeCount);

                // Zombies
                var zombie = t2.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void HeaderAndFreeListRoundTrip()
        {
            string path = "raven.db";
            File.Delete(path);

            int order = 4;
            int nodeCountBefore;

            // Create and free some nodes
            using (var t = new BTree(path, order))
            {
                for (int i = 1; i <= 20; i++) t.Insert(i, i);
                t.Delete(1, 1);
                t.Delete(2, 2);
                nodeCountBefore = t.Header.NodeCount;
            }

            // Reopen and ensure header/free list was preserved
            using (var t2 = new BTree(path, order))
            {
                Assert.AreEqual(order, t2.Header.Order);
                // Reuse a free slot on insert
                t2.Insert(1000, 1000);
                Assert.AreEqual(nodeCountBefore, t2.Header.NodeCount, "NodeCount should not increase if free slot reused");

                var zombies = t2.GetZombies();
                Assert.AreEqual(0, zombies.Count, "Zombies on reopen");
            }

            File.Delete(path);
        }


        [TestMethod]
        public void SmokeTestOne()
        {
            string path = "rose.db";
            File.Delete(path);

            int order = 10;

            // Session 1: create, insert, validate in-memory
            using (var tree = new BTree(path, order))
            {
                tree.Insert(10, 100);
                tree.Insert(20, 200);
                tree.Insert(30, 300);

                Element e;
                Assert.IsTrue(tree.TrySearch(20, out e), "Inserted key 20 must be found");
                Assert.AreEqual(200, e.Data);

                var keys = tree.GetKeys();
                CollectionAssert.AreEqual(new List<int> { 10, 20, 30 }, keys);

                // Delete one key to exercise deletion path
                tree.Delete(20, 200);
                Assert.IsFalse(tree.TrySearch(20, out e), "Deleted key 20 must not be found");

                tree.ValidateIntegrity();
            }

            // Session 2: reopen and verify persistence
            using (var tree = new BTree(path, order))
            {
                Element e;
                Assert.IsTrue(tree.TrySearch(10, out e), "Key 10 must persist after reopen");
                Assert.AreEqual(100, e.Data);

                Assert.IsTrue(tree.TrySearch(30, out e), "Key 30 must persist after reopen");
                Assert.AreEqual(300, e.Data);

                var keys = tree.GetKeys();
                CollectionAssert.AreEqual(new List<int> { 10, 30 }, keys);

                tree.ValidateIntegrity();
                var zombies = tree.GetZombies();
                Assert.AreEqual(0, zombies.Count, "No zombie pages expected");
            }

            File.Delete(path);
        }

    }
}
