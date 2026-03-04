namespace DiskTwo
{
    /// <summary>
    /// Implements a top-down bulk-loading algorithm to create a balanced B-Tree 
    /// from a sorted list of elements. This is significantly faster than inserting keys one-by-one.
    /// </summary>
    public class TreeBuilder
    {
        private int Order;
        private int LeafTarget;   // Target number of keys per leaf based on fill factor
        private double LeafFill;

        private TreeManager Manager;

        /// <summary>
        /// Initializes the builder with specific fill constraints.
        /// </summary>
        public TreeBuilder(int order = 64, double leafFill = 0.8)
        {
            Order = order;
            LeafFill = Math.Clamp(leafFill, 0.5, 1.0);

            // Calculate how many keys we aim to put in each leaf to allow for future growth
            int target = (int)((Order - 1) * LeafFill);
            LeafTarget = Math.Clamp(target, 1, Order - 1);

            if (Order < 4)
            {
                throw new ArgumentException("Order must be at least 4.");
            }
        }


        /// <summary>
        /// The entry point for building a tree. It validates the input, 
        /// manages the lifecycle of the TreeManager, and saves the final Root ID.
        /// </summary>
        public void CreateFromSorted(List<Element> keys, string path)
        {
            if (keys == null || keys.Count == 0) return;
            if (!Util.IsSortedList(keys)) throw new ArgumentException(nameof(keys), "Must be sorted.");

            // Using block ensures the file is closed even if Build() fails
            using (Manager = new TreeManager(path, Order))
            {
                int? rootId = Build(keys, 0, keys.Count - 1);
                if (rootId.HasValue)
                {
                    Manager.Header.RootId = rootId.Value;
                    Manager.SaveHeader();
                }
            }
        }

        /// <summary>
        /// Recursive entry point that decides whether to create a Leaf or an Internal node
        /// based on the remaining number of keys and the calculated tree height.
        /// </summary>
        private int Build(List<Element> keys, int start, int end)
        {
            int count = end - start + 1;
            if (count <= 0) return -1;

            // Determine how tall the subtree for this range of keys needs to be
            // This prevents the "Infinite Internal Recursion".
            int height = CalculateHeight(count, Order);

            // Base Case: If the height is 1 or keys fit in a target leaf, make a leaf node.
            if (count <= LeafTarget || height <= 1)
            {
                return CreateLeaf(keys, start, count);
            }

            // Recursive Case: Build internal nodes to act as parents for subtrees
            return CreateInternal(keys, start, end);
        }

        /// <summary>
        /// Creates a leaf node and fills it with a contiguous range of keys from the list.
        /// </summary>
        private int CreateLeaf(List<Element> keys, int start, int count)
        {
            BNode node = InitializeNode(isLeaf: true);
            for (int i = 0; i < Math.Min(count, Order - 1); i++) node.Keys[i] = keys[start + i];
            node.NumKeys = Math.Min(count, Order - 1);
            return FinalizeNode(node);
        }

        /// <summary>
        /// Manages the complex logic of splitting a range of keys across multiple children.
        /// It builds children subtrees and promotes "separator keys" to the current node.
        /// </summary>
        private int CreateInternal(List<Element> keys, int start, int end)
        {
            BNode node = InitializeNode(isLeaf: false);
            int remaining = end - start + 1;
            int current = start;

            int totalHeight = CalculateHeight(remaining, Order);
            int childSubtreeCapacity = GetMaxSubtreeSize(totalHeight - 1, Order);

            for (int i = 0; i < Order; i++)
            {
                // 1. Determine how many keys to give to the next child
                int childCount = Math.Min(remaining, childSubtreeCapacity);

                // 2. THE OVERFLOW PREVENTER: Logic to ensure we don't put all keys into one child,
                // which would cause an infinite recursive loop (StackOverflow).
                if (childCount == remaining && i == 0)
                {
                    // Force a split so at least one separator and two children are created
                    childCount = Math.Min(remaining - 1, childSubtreeCapacity);
                }
                else if (remaining == childCount + 1)
                {
                    // Leave room for a trailing child so the separator has something to point to
                    childCount--;
                }

                // Recursively build the child subtree
                node.Kids[i] = Build(keys, current, current + childCount - 1);
                current += childCount;
                remaining -= childCount;

                // 3. Promote a separator key if we have keys left and space in the node
                if (remaining > 0 && i < Order - 1)
                {
                    node.Keys[i] = keys[current++];
                    node.NumKeys++;
                    remaining--;

                    // If no keys are left, the loop terminates; the last child was Kid[i]
                    if (remaining == 0) break;
                }
                else break;
            }

            return FinalizeNode(node);
        }

        // --- Node Helpers ---

        /// <summary>
        /// Calculates the maximum number of elements a subtree of a given height can hold.
        /// </summary>
        private int GetMaxSubtreeSize(int height, int order)
        {
            if (height <= 0) return 0;
            long capacity = (long)Math.Pow(order, height - 1) * LeafTarget;
            return (int)Math.Min(capacity, (long)int.MaxValue);
        }


        private BNode InitializeNode(bool isLeaf)
        {
            return Manager.CreateNode(isLeaf); // Builder just asks for a node
        }

        /// <summary>
        /// Writes the node to disk and returns its Disk ID (offset pointer).
        /// </summary>
        private int FinalizeNode(BNode node)
        {
            Manager.SaveToDisk(node);
            return node.Id;
        }

        /// <summary>
        /// Determines the necessary B-Tree height to accommodate the given number of elements
        /// while respecting the LeafFill/LeafTarget constraints.
        /// </summary>
        private int CalculateHeight(int totalElements, int order)
        {
            if (totalElements <= 0) return 0;

            // If it fits in a single leaf (using the Order limit), height is 1
            if (totalElements <= order - 1) return 1;

            int height = 1;
            long capacity = LeafTarget; // The base level (leaves) can hold this many

            // Keep adding internal levels (multiplied by order) until we can fit everything
            while (totalElements > capacity)
            {
                height++;
                capacity *= order;

                // Logical ceiling for safety.
                if (height > 10) break; // Safety against infinite loops.
            }
            return height;
        }


    }
}
