using DiskTwo;

namespace UnitTestOne
{
    [TestClass]
    public sealed class UnitOne
    {
        /// <summary>
        /// An insertion test for a small number of items.  Ten items or less.
        /// Test seiches. Test one deletion.  Test one min and one max.
        /// Ten items or less.  This is a general purpose sanity check for the code.
        /// </summary>
        [TestMethod]
        public void SimpleInsertEight()
        {
            string outFileName = "rain.bin";
            File.Delete(outFileName);

            // 1. Create the B-Tree (Order 4, meaning max 3 keys per node)
            // This will create or overwrite the file.
            using (var tree = new BTree(outFileName, 4))
            {

                // 2. Insert elements
                tree.Insert(10, 100);
                tree.Insert(20, 200);
                tree.Insert(30, 300); // Node 0: [10, 20, 30] (Full)

                // Inserting 40 will cause a split: 20 promoted to a new root.
                tree.Insert(40, 400);

                // A B-Tree of order 4 with keys 10, 20, 30, 40 now looks like:
                // Root (Disk 0): [20]
                // Left Child (Disk 1): [10]
                // Right Child (Disk 2): [30, 40]

                tree.Insert(50, 500);
                tree.Insert(60, 600);
                tree.Insert(70, 700);
                tree.Insert(80, 800);

                // 3. Search for an element
                int searchKey = 50;
                Element item;
                Assert.IsTrue(tree.TrySearch(searchKey, out item));

                // 3a. Sanity Checks
                List<int> a = tree.GetKeys();
                Assert.IsTrue(a.Count > 0);
                Assert.IsTrue(Util.IsSorted(a));
                Assert.IsFalse(Util.HasDuplicate(a));

                // 4. Delete an element
                tree.Delete(10, 100);
                searchKey = 10;
                Assert.IsFalse(tree.TrySearch(searchKey, out item));


                // 5. Find Min/Max
                Element? max = tree.FindMax();
                Assert.IsTrue(max.HasValue);
                if (max.HasValue)
                {
                    Assert.AreEqual(max.Value.Key, 80);
                    Assert.AreEqual(max.Value.Data, 800);
                }

                Element? min = tree.FindMin();
                Assert.IsTrue(min.HasValue);
                if (min.HasValue)
                {
                    Assert.AreEqual(min.Value.Key, 20);
                    Assert.AreEqual(min.Value.Data, 200);
                }

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(outFileName);
        }

        /// <summary>
        /// A sequential insertion test for testing split nodes.
        /// </summary>
        [TestMethod]
        public void MediumInsertFifty()
        {
            string outFileName = "bagel.bin";
            File.Delete(outFileName);

            int order = 4; // Small order forces lots of splits
            using (var tree = new BTree(outFileName, order))
            {
                // Sequential Insertion 
                for (int i = 1; i <= 50; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }

                Assert.IsTrue(tree.Header.RootId >= 0, "RootId lost");

                // Verification 
                for (int i = 1; i <= 50; i++)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), "Key missing {i}");
                }

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(outFileName);
        }


        /// <summary>
        /// A sequential insertion test for testing split nodes.
        /// </summary>
        [TestMethod]
        public void MediumInsertHundred()
        {
            string outFileName = "red.bin";
            File.Delete(outFileName);

            int order = 4; // Small order forces lots of splits
            using (var tree = new BTree(outFileName, order))
            {
                // Sequential Insertion 
                for (int i = 1; i <= 100; i++)
                {
                    // Using i*10 as data just to distinguish Key from Data
                    tree.Insert(i, i * 10);
                }

                Assert.IsTrue(tree.Header.RootId >= 0, "RootId lost");

                // Verification 
                for (int i = 1; i <= 100; i++)
                {
                    Element item;
                    Assert.IsTrue(tree.TrySearch(i, out item), "Key missing {i}");
                }

                // Zombies
                var zombie = tree.GetZombies();
                Assert.AreEqual(0, zombie.Count, "Zombies");
            }

            File.Delete(outFileName);
        }

    }
}
