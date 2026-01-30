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

            int order = 4; // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order))
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
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Key missing {i}");
                }

                // Deletion
                tree.Delete(1, 10);
                Assert.IsTrue(tree.Header.RootId > 0);

                // Zombies
                var list = tree.GetZombies();
                Assert.AreEqual(0, list.Count, "Zombies");
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

            int order = 5; // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order))
            {
                // Sequential Insertion 
                for (int i = 1; i <= 10; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }

                Assert.IsTrue(tree.Header.RootId >= 0, "RootId lost");

                for (int i = 1; i <= 10; i++)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Key missing {i}");
                }

                // Deletion
                tree.Delete(1, 10);
                Assert.IsTrue(tree.Header.RootId >= 0, "Root lost");

                // Zombies
                var list = tree.GetZombies();
                Assert.AreEqual(0, list.Count, "Zombies");
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

            int order = 10; // Small order forces lots of splits
            using (var tree = new DiskTwo.BTree(outFileName, order))
            {
                // Sequential Insertion 
                for (int i = 1; i <= 10; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }

                Assert.IsTrue(tree.Header.RootId >= 0, "RootId lost");

                for (int i = 1; i <= 10; i++)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Key missing {i}");
                }

                // Deletion
                tree.Delete(1, 10);
                Assert.IsTrue(tree.Header.RootId > 0);

                // Zombies
                var list = tree.GetZombies();
                Assert.AreEqual(0, list.Count, "Zombies");
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
                for (int i = 1; i <= 10; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }

                for (int i = 1; i <= 10; i += 10)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), $"Key missing {i}");
                }

                // Deletion
                tree.Delete(1, 10);

                // Zombies
                var list = tree.GetZombies();
                Assert.AreEqual(0, list.Count, "Zombies");
            }

            File.Delete(outFileName);
        }


    }
}
