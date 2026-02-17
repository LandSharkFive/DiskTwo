using DiskTwo;

namespace UnitTestTwo
{
    [TestClass]
    public sealed class UnitTwo
    {
        /// <summary>
        /// A sequential insertion and deletion test.
        /// </summary>
        [TestMethod]
        public void SimpleDeleteOrderFour()
        {
            string outFileName = "orange.bin";
            File.Delete(outFileName);

            // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order: 4))
            {
                // Sequential Insertion 
                for (int i = 1; i <= 10; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }

                Assert.IsTrue(tree.Header.RootId > 0);

                for (int i = 1; i <= 10; i++)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Missing Key {i}");
                }

                // Deletion
                tree.Delete(1, 10);
                Assert.IsTrue(tree.Header.RootId > 0);

                // Zombies
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(outFileName);
        }

        /// <summary>
        /// A sequential insertion and deletion test.
        /// </summary>
        [TestMethod]
        public void SimpleDeleteOrderFive()
        {
            string outFileName = "dock.bin";
            File.Delete(outFileName);

            // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order: 5))
            {
                // Sequential Insertion 
                for (int i = 1; i <= 10; i++) tree.Insert(i, i * 10);

                Assert.IsTrue(tree.Header.RootId >= 0, "RootId lost");

                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(10, count, "Missing Keys");

                for (int i = 1; i <= 10; i++)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Missing Key {i}");
                }

                // Deletion
                tree.Delete(1, 10);
                Assert.IsTrue(tree.Header.RootId >= 0, "Root lost");
                count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(9, count, "Missing Keys");

                // Zombies
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(outFileName);
        }


        /// <summary>
        /// A sequential insertion and deletion test.
        /// </summary>
        [TestMethod]
        public void SimpleDeleteOrderTen()
        {
            string outFileName = "bear.bin";
            File.Delete(outFileName);

            // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order: 10))
            {
                // Sequential Insertion 
                List<int> data = new List<int>();
                for (int i = 1; i <= 10; i++) data.Add(i);
                
                foreach (int i in data) tree.Insert(i, i * 10);

                Assert.IsTrue(tree.Header.RootId >= 0, "RootId lost");
                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Missing Keys");

                foreach (int i in data)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Missing Key {i}");
                }

                // Deletion
                tree.Delete(1, 10);
                Assert.IsTrue(tree.Header.RootId >= 0);
                count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count - 1, count, "Missing Keys");

                // Zombies
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(outFileName);
        }


        /// <summary>
        /// A sequential insertion and deletion test.
        /// </summary>
        [TestMethod]
        public void SimpleDeleteOrderSixteen()
        {
            string outFileName = "bubble.bin";
            File.Delete(outFileName);

            using (var tree = new DiskTwo.BTree(outFileName, order: 16))
            {
                // Sequential Insertion 
                List<int> data = new List<int>();
                for (int i = 1; i <= 10; i++)  data.Add(i);

                foreach (int i in data) tree.Insert(i, i * 10);

                int count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count, count, "Missing Keys");

                foreach (int i in data)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Missing Key {i}");
                }

                // Deletion
                tree.Delete(1, 10);

                // Verify one key deleted.
                count = tree.CountKeys(tree.Header.RootId);
                Assert.AreEqual(data.Count - 1, count, "Missing Keys");

                // Zombies
                Assert.AreEqual(0, tree.CountZombies(), "Zombies");
            }

            File.Delete(outFileName);
        }


    }
}
