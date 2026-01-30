namespace DiskTwo
{
    public class BulkLoader
    {
        private BTree MyTree { get; set; }
        private int Order { get; set; }
        private double LeafFactor { get; set; }
        private double IndexFactor { get; set; }

        /// <summary>
        /// Constructor for BulkLoader.
        /// </summary>
        public BulkLoader(BTree tree, int order = 60, double leafFactor = 0.8, double indexFactor = 0.8)
        {
            MyTree = tree;
            Order = order;
            LeafFactor = leafFactor;
            IndexFactor = indexFactor;
        }


        /// <summary>
        /// Bulk Load is the entry point that builds a B-Tree from a sorted list of keys using a "spine" of active nodes. Uses configurable LeafFactor 
        /// and IndexFactor to leave specific amounts of free space in nodes for future growth. Iterates through sorted data to build 
        /// the tree bottom-up, concluding with a root finalization step.
        /// </summary>
        public void BulkLoad(List<int> sortedKeys)
        {
            if (sortedKeys == null || sortedKeys.Count == 0) return;

            int leafMax = (int)Math.Max(1, (Order - 1) * LeafFactor);
            int indexMax = (int)Math.Max(1, (Order - 1) * IndexFactor);

            List<BNode> spine = new List<BNode>();

            foreach (int key in sortedKeys)
            {
                AddKeyToSpine(0, key, spine, leafMax, indexMax);
            }

            FinalizeAndFixRoot(spine);
        }


        /// <summary>
        /// Add key to spine manages the current "active" node at a specific level, adding keys until the fill-limit is reached. 
        /// When a node hits its limit, it is committed to disk, and the next key is promoted to the parent level. 
        /// Ensures new nodes are correctly initialized with a unique disk ID before becoming the new active level head.
        /// </summary>
        private void AddKeyToSpine(int level, int key, List<BNode> spine, int leafMax, int indexMax)
        {
            if (spine.Count <= level)
            {
                BNode newNode = new BNode(Order) { IsLeaf = (level == 0), Id = MyTree.GetNextId() };
                spine.Add(newNode);
            }

            BNode currentNode = spine[level];
            int limit = currentNode.IsLeaf ? leafMax : indexMax;

            if (currentNode.NumKeys < limit)
            {
                // Correctly initializing the Element object
                currentNode.Keys[currentNode.NumKeys] = new Element(key, 0);
                currentNode.NumKeys++;
            }
            else
            {
                // 1. Write the full node to disk.
                MyTree.DiskWrite(currentNode);
                int finishedNodeId = currentNode.Id;

                // 2. Create the new node (it starts EMPTY)
                BNode newNode = new BNode(Order) { IsLeaf = (level == 0), Id = MyTree.GetNextId() };
                spine[level] = newNode;

                // 3. DO NOT add 'key' to newNode.Keys[0]. 
                // Instead, promote it immediately.
                PromoteToParent(level + 1, key, finishedNodeId, spine, indexMax);
            }
        }


        /// <summary>
        /// Promote to parent handles the recursive upward promotion of separator keys to internal index levels. Manages the creation of new 
        /// internal levels when the tree needs to grow in height. Correctly links child pointers (Kids) to ensure the path 
        /// from the index down to the leaves remains intact.
        /// </summary>
        private void PromoteToParent(int level, int key, int leftChildId, List<BNode> spine, int indexMax)
        {
            // Case 1: Level doesn't exist yet (Tree is growing taller)
            if (spine.Count <= level)
            {
                BNode newParent = new BNode(Order) { IsLeaf = false, Id = MyTree.GetNextId() };
                newParent.Kids[0] = leftChildId; // Correctly link to the only child so far
                spine.Add(newParent);
            }

            BNode parent = spine[level];

            // Case 2: Current internal node in spine has space
            if (parent.NumKeys < indexMax)
            {
                parent.Keys[parent.NumKeys] = new Element(key, 0);
                // The right child of this key is the current active node from the level below
                parent.Kids[parent.NumKeys + 1] = spine[level - 1].Id;
                parent.NumKeys++;
            }
            // Case 3: Current internal node in spine is full
            else
            {
                MyTree.DiskWrite(parent);
                int finishedInternalId = parent.Id;

                BNode newNode = new BNode(Order) { IsLeaf = false, Id = MyTree.GetNextId() };

                // The first child pointer of the new node is the current child from level below
                newNode.Kids[0] = spine[level - 1].Id;
                newNode.NumKeys = 0;
                spine[level] = newNode;

                // Promote 'key' up. Do NOT save it in 'newNode'.
                PromoteToParent(level + 1, key, finishedInternalId, spine, indexMax);
            }
        }

        /// <summary>
        /// Finalize and fix root flushes the remaining "spine" nodes from memory to disk once the input data is exhausted. 
        /// Resolves the final child pointers for each level and identifies the top-most node to be the new tree root. 
        /// Updates the B-Tree header with the correct RootId to ensure the structure is immediately ready for use.
        /// </summary>
        private void FinalizeAndFixRoot(List<BNode> spine)
        {
            int lastId = -1;

            for (int i = 0; i < spine.Count; i++)
            {
                BNode node = spine[i];

                // If a node is empty and NOT the root, it shouldn't exist in the final tree.
                if (node.NumKeys == 0 && i < spine.Count - 1)
                {
                    // Skip it, and don't update lastId, effectively "dropping" this ghost node.
                    continue;
                }

                if (lastId != -1)
                {
                    node.Kids[node.NumKeys] = lastId;
                }

                // ONLY write the node if it has keys, OR if it's the very last node (the root)
                MyTree.DiskWrite(node);
                lastId = node.Id;
            }

            MyTree.Header.RootId = lastId;
            MyTree.SaveHeader();
        }


    }
}