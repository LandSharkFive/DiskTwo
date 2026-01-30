using System;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DiskTwo
{
    /// <summary>
    /// Represents the B-Tree structure.
    /// </summary>
    public class BTree : IDisposable
    {
        private string MyFileName { get; set; }

        private FileStream MyFileStream;

        private readonly Stack<int> FreeList = new Stack<int>();

        private byte[] ZeroBuffer;

        private const int HeaderSize = 4096;

        private const int MagicConstant = 0x42542145;

        public BTreeHeader Header;


        /// <summary>
        /// Constructor.
        /// </summary>
        public BTree(string fileName, int order)
        {
            MyFileName = fileName;

            // 1. Always open the stream first
            MyFileStream = new FileStream(MyFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            if (MyFileStream.Length > 0)
            {
                // 2. Existing file: Trust the disk
                LoadHeader();
                LoadFreeList();
            }
            else
            {
                // 3. New file: Trust the constructor arguments
                Header.Magic = MagicConstant;
                Header.Order = order;
                Header.PageSize = BNode.CalculateNodeSize(Header.Order);
                Header.RootId = -1;
                Header.NodeCount = 0;
                SaveHeader();
            }
        }

        // ------ HELPER METHODS ------

        public void Dispose()
        {
            // Simply call your existing Close logic to persist data
            Close();
        }

        public void Close()
        {
            // Ensure we don't try to close an already closed stream
            if (MyFileStream != null)
            {
                SaveFreeList();
                SaveHeader();
                MyFileStream.Close();
                MyFileStream = null; // Mark as closed
            }
        }

        /// <summary>
        /// Calculates the byte offset in the file for a given disk position (record index).
        /// </summary>
        private long CalculateOffset(int disk)
        {
            int sizeOfBtNode = BNode.CalculateNodeSize(Header.Order);
            return ((long)sizeOfBtNode * disk) + HeaderSize;
        }


        // -------- DISK I/O METHODS -----------

        /// <summary>
        /// Retrieves stored data from physical storage.
        /// </summary>
        public BNode DiskRead(int disk)
        {
            if (disk < 0)
            {
                throw new ArgumentOutOfRangeException("CRITICAL: Reading negative disk id.");
            }

            BNode readNode = new BNode(Header.Order);
            long offset = CalculateOffset(disk);

            MyFileStream.Seek(offset, SeekOrigin.Begin);

            using (var reader = new BinaryReader(MyFileStream, System.Text.Encoding.UTF8, true))
            {
                readNode.IsLeaf = reader.ReadInt32() == 1;
                readNode.NumKeys = reader.ReadInt32();
                readNode.Id = reader.ReadInt32();

                // CHANGED: Read 'Order' elements instead of 'Order - 1' 
                // to match the BNode's padded array size.
                for (int i = 0; i < Header.Order; i++)
                {
                    readNode.Keys[i].Key = reader.ReadInt32();
                    readNode.Keys[i].Data = reader.ReadInt32();
                }

                // CHANGED: Read 'Order + 1' children instead of 'Order'.
                for (int i = 0; i < Header.Order + 1; i++)
                {
                    readNode.Kids[i] = reader.ReadInt32();
                }
            }
            return readNode;
        }

        /// <summary>
        /// Write a node to disk using the fixed binary layout described in DiskRead.
        /// Ensure Header.Order and BNode layout remain compatible with previously written files.
        /// </summary>
        public void DiskWrite(BNode node)
        {
            long offset = CalculateOffset(node.Id);
            MyFileStream.Seek(offset, SeekOrigin.Begin);

            using (var writer = new BinaryWriter(MyFileStream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(node.IsLeaf ? 1 : 0);
                writer.Write(node.NumKeys);
                writer.Write(node.Id);

                // CHANGED: Write 'Order' elements to match BNode capacity.
                for (int i = 0; i < Header.Order; i++)
                {
                    writer.Write(node.Keys[i].Key);
                    writer.Write(node.Keys[i].Data);
                }

                // CHANGED: Write 'Order + 1' children pointers.
                for (int i = 0; i < Header.Order + 1; i++)
                {
                    writer.Write(node.Kids[i]);
                }
            }
            MyFileStream.Flush();
        }

        /// <summary>
        /// Wipe a disk sector by filling with zeros.
        /// </summary>
        public void ZeroOutDiskSpace(int id)
        {
            if (id < 0)
            {
                return;
            }

            // Initialize the buffer if it's the first time
            if (ZeroBuffer == null)
            {
                int nodeSize = BNode.CalculateNodeSize(Header.Order);
                ZeroBuffer = new byte[nodeSize];
            }

            long offset = CalculateOffset(id);
            MyFileStream.Seek(offset, SeekOrigin.Begin);
            MyFileStream.Write(ZeroBuffer, 0, ZeroBuffer.Length);
            MyFileStream.Flush();
        }


        // --- HEADER METHODS ---

        /// <summary>
        /// Writes the B-Tree header to disk.
        /// </summary>
        public void SaveHeader()
        {
            byte[] buffer = new byte[4096];
            using (var ms = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(ms))
            {
                Header.Write(writer);
            }

            MyFileStream.Seek(0, SeekOrigin.Begin);
            MyFileStream.Write(buffer, 0, buffer.Length);
            MyFileStream.Flush();
        }

        /// <summary>
        /// Load the B-Tree header from disk.
        /// </summary>
        public void LoadHeader()
        {
            MyFileStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new BinaryReader(MyFileStream, System.Text.Encoding.UTF8, true))
            {
                Header = BTreeHeader.Read(reader);
            }

            if (Header.Magic != MagicConstant)
                throw new Exception("Invalid B-Tree File");
        }


        // --- SEARCH METHODS ---

        /// <summary>
        /// Searches the tree for a key.
        /// </summary>
        public bool TrySearch(int key, out Element result)
        {
            result = default; // Initialize
            if (Header.RootId == -1) return false;

            BNode rootNode = DiskRead(Header.RootId);
            return TrySearchRecursive(rootNode, key, out result);
        }

        /// <summary>
        /// Search the tree recursively for a key.
        /// </summary>
        private bool TrySearchRecursive(BNode node, int key, out Element result)
        {
            int i = 0;
            while (i < node.NumKeys && key > node.Keys[i].Key) i++;

            if (i < node.NumKeys && key == node.Keys[i].Key)
            {
                result = node.Keys[i];
                return true;
            }

            if (node.IsLeaf)
            {
                result = default;
                return false;
            }

            BNode child = DiskRead(node.Kids[i]);
            return TrySearchRecursive(child, key, out result);
        }

        // ------- INSERT METHODS --------

        /// <summary>
        /// Inserts a new element into the tree. 
        /// </summary>
        public void Insert(int key, int data)
        {
            if (Header.RootId == -1)
            {
                BNode firstNode = new BNode(Header.Order) { IsLeaf = true, Id = GetNextId() };
                Header.RootId = firstNode.Id;
                firstNode.Keys[0] = new Element(key, data);
                firstNode.NumKeys = 1;
                DiskWrite(firstNode);
                SaveHeader(); // IMPORTANT
                return;
            }

            BNode rootNode = DiskRead(Header.RootId);
            if (rootNode.NumKeys == Header.Order - 1)
            {
                // Root is full, tree grows in height
                BNode newRoot = new BNode(Header.Order) { IsLeaf = false, Id = GetNextId() };
                newRoot.Kids[0] = rootNode.Id;

                // Split the old root
                SplitChild(newRoot, 0, true);

                // Refresh to get the updated pointers from SplitChild
                newRoot = DiskRead(newRoot.Id);
                InsertNonFull(newRoot, new Element(key, data));
            }
            else
            {
                InsertNonFull(rootNode, new Element(key, data));
            }

            SaveHeader();
        }


        // --- INSERTION HELPERS ---

        /// <summary>
        /// Insert Non Full recursively descends the tree to find the appropriate leaf for a new key while ensuring the path remains "split-ready." 
        /// If a child node is at maximum capacity, it is split before the descent to maintain B-Tree invariants. Shifts keys and children 
        /// within a leaf to maintain sorted order before performing the final disk write.
        /// </summary>
        private void InsertNonFull(BNode node, Element key)
        {
            int pos = node.NumKeys - 1;

            if (node.IsLeaf)
            {
                // Find correct position and shift keys
                while (pos >= 0 && key.Key < node.Keys[pos].Key)
                {
                    node.Keys[pos + 1] = node.Keys[pos];
                    pos--;
                }
                node.Keys[pos + 1] = key;
                node.NumKeys++;
                DiskWrite(node);
            }
            else
            {
                // Find the child node to descend into
                while (pos >= 0 && key.Key < node.Keys[pos].Key)
                {
                    pos--;
                }
                pos++; // pos is the index of the child to descend (Kids[pos])

                if (pos < 0 || pos > node.NumKeys)
                {
                    throw new Exception($"Invalid child index {pos} in node {node.Id}");
                }
                BNode child = DiskRead(node.Kids[pos]);

                if (child.NumKeys == Header.Order - 1)
                {
                    // Child is full, must split it
                    SplitChild(node, pos, isRootSplit: false);

                    // Get the updated parent node after the split
                    node = DiskRead(node.Id);

                    // Determine which side of the promoted key the new key goes
                    if (key.Key > node.Keys[pos].Key)
                    {
                        pos++;
                    }
                }

                // Recursive call on the appropriate child
                child = DiskRead(node.Kids[pos]);
                InsertNonFull(child, key);
            }
        }

        /// <summary>
        /// Split Child divides a full child node by moving the upper half of its keys and children into a new sibling node. 
        /// Promotes the median key to the parent and updates all affected disk pointers. Handles root-specific logic 
        /// to ensure the tree height increases correctly without orphaning the old root ID.
        /// </summary>
        private void SplitChild(BNode x, int pos, bool isRootSplit)
        {
            BNode y = DiskRead(x.Kids[pos]); // The full node
            BNode z = new BNode(Header.Order) { IsLeaf = y.IsLeaf, Id = GetNextId() };

            int t = (Header.Order + 1) / 2;
            int medianIdx = t - 1;

            if (isRootSplit)
            {
                // Capture the ID that is about to be vacated
                int vacatedId = x.Kids[pos];

                // LOGIC FIX: When called as part of a root split initiated by Insert, x is the newly allocated root container.
                // We must ensure the ID it takes doesn't 'orphan' the old root's disk slot.
                int oldRootId = x.Id;
                y.Id = GetNextId(); // Y moves to a new physical location
                x.Kids[pos] = y.Id;
                Header.RootId = oldRootId; // Ensure Header points to the new root container

                // If the old root ID isn't being used by the new root, free it
                if (vacatedId != x.Id && vacatedId != y.Id && vacatedId != z.Id)
                {
                    FreeList.Push(vacatedId);
                }
            }

            // Move keys/kids to Z (Right Sibling)
            int keysToMove = y.NumKeys - t;
            z.NumKeys = keysToMove;

            for (int j = 0; j < keysToMove; j++)
            {
                z.Keys[j] = y.Keys[medianIdx + 1 + j];
                y.Keys[medianIdx + 1 + j] = default;
            }

            if (!y.IsLeaf)
            {
                for (int j = 0; j <= keysToMove; j++)
                {
                    z.Kids[j] = y.Kids[medianIdx + 1 + j];
                    y.Kids[medianIdx + 1 + j] = -1;
                }
            }

            y.NumKeys = medianIdx;

            // Shift Keys in Parent (x)
            for (int j = x.NumKeys - 1; j >= pos; j--)
            {
                x.Keys[j + 1] = x.Keys[j];
            }

            // Shift Children in Parent (x)
            for (int j = x.NumKeys; j >= pos + 1; j--)
            {
                x.Kids[j + 1] = x.Kids[j];
            }

            // Promote median key to parent
            x.Keys[pos] = y.Keys[medianIdx];
            x.Kids[pos] = y.Id;
            x.Kids[pos + 1] = z.Id;

            y.Keys[medianIdx] = default;
            x.NumKeys++;

            // Write updates
            DiskWrite(y);
            DiskWrite(z);
            DiskWrite(x);

            if (isRootSplit)
            {
                SaveHeader();
            }
        }


        /// <summary>
        /// Allocates a new unique ID by claiming the next free slot on disk.
        /// </summary>
        public int GetNextId()
        {
            // If we have a hole in the file, reuse it!
            if (FreeList.Count > 0)
            {
                return FreeList.Pop();
            }

            // Append to end of file.
            int nextPos = Header.NodeCount;
            Header.NodeCount++;
            return nextPos;
        }

        // ------ DELETE METHODS ------

        /// <summary>
        /// Delete a key from the tree.
        /// </summary>
        public void Delete(int key, int data)
        {
            if (Header.RootId == -1) return;

            Element deleteKey = new Element(key, data);
            BNode rootNode = DiskRead(Header.RootId);

            // 1. Perform the recursive deletion
            DeleteSafe(rootNode, deleteKey);

            // 2. IMPORTANT: Persist any changes made to the rootNode during recursion
            // If DeleteSafe emptied it, we need that '0 keys' state on the disk now.
            DiskWrite(rootNode);

            // 3. RE-READ to ensure we are looking at the absolute latest state
            BNode finalRoot = DiskRead(Header.RootId);

            // 4. Root Collapse: If the root is a "Ghost" (0 keys, internal), bypass it.
            if (finalRoot.NumKeys == 0 && !finalRoot.IsLeaf)
            {
                int oldId = Header.RootId;

                // Promote the first child to be the new King
                Header.RootId = finalRoot.Kids[0];

                // Save the Header immediately so the Audit knows where to start
                SaveHeader();

                // Clean up the evidence of the old root
                ZeroOutDiskSpace(oldId);
                FreeList.Push(oldId);
            }
        }

        // --- MIN AND MAX HELPERS ---
        // Min and Max are similar to Search.

        public Element? FindMax()
        {
            if (Header.RootId == -1) return null;
            BNode rootNode = DiskRead(Header.RootId);
            return FindMaxRecursive(rootNode);
        }

        private Element? FindMaxRecursive(BNode node)
        {
            if (node.IsLeaf)
            {
                return node.Keys[node.NumKeys - 1];
            }
            else
            {
                BNode child = DiskRead(node.Kids[node.NumKeys]);
                return FindMaxRecursive(child);
            }
        }

        public Element? FindMin()
        {
            if (Header.RootId == -1) return null;
            BNode rootNode = DiskRead(Header.RootId);
            return FindMinRecursive(rootNode);
        }

        private Element? FindMinRecursive(BNode node)
        {
            if (node.IsLeaf)
            {
                return node.Keys[0];
            }
            else
            {
                BNode child = DiskRead(node.Kids[0]);
                return FindMinRecursive(child);
            }
        }

        // ------ DELETE HELPERS -------

        /// <summary>
        /// Remove the largest key from the subtree rooted at node.
        /// </summary>
        private Element DeleteMax(BNode node)
        {
            int t = (Header.Order + 1) / 2;

            if (node.IsLeaf)
            {
                Element result = node.Keys[node.NumKeys - 1];
                node.Keys[node.NumKeys - 1] = new Element { Key = -1, Data = -1 };
                node.NumKeys--;
                DiskWrite(node);
                return result;
            }

            // Descend to the rightmost child: node.Kids[node.NumKeys]
            BNode child = DiskRead(node.Kids[node.NumKeys]);

            if (child.NumKeys == t - 1)
            {
                // Child is too thin. For the rightmost child we examine the left sibling first.
                // If the left sibling can lend a key, borrow; otherwise merge.
                BNode leftSibling = DiskRead(node.Kids[node.NumKeys - 1]);
                if (leftSibling.NumKeys >= t)
                {
                    BorrowFromLeftSibling(node, node.NumKeys);
                }
                else
                {
                    MergeChildren(node, node.NumKeys - 1);
                    node = DiskRead(node.Id); // RE-SYNC PARENT
                    child = DiskRead(node.Kids[node.NumKeys]);
                }
                // After a merge, node.NumKeys decreased, so the rightmost index changed
                child = DiskRead(node.Kids[node.NumKeys]);
            }

            return DeleteMax(child);
        }

        /// <summary>
        /// Remove the smallest key from the subtree rooted at node.
        /// </summary>
        private Element DeleteMin(BNode node)
        {
            int t = (Header.Order + 1) / 2;

            // Base Case: We hit the leaf. This is safe to delete because 
            // the recursive steps above ensured this leaf has at least t keys.
            if (node.IsLeaf)
            {
                Element result = node.Keys[0];
                // Shift keys left to fill the hole at index 0
                for (int i = 0; i < node.NumKeys - 1; i++)
                    node.Keys[i] = node.Keys[i + 1];

                node.Keys[node.NumKeys - 1] = new Element { Key = -1, Data = -1 };
                node.NumKeys--;
                DiskWrite(node);
                return result;
            }

            // Recursive Case: We need to go to the leftmost child
            BNode child = DiskRead(node.Kids[0]);

            if (child.NumKeys == t - 1)
            {
                // Child is too thin! We must beef it up before descending.
                BNode rightSibling = DiskRead(node.Kids[1]);
                if (rightSibling.NumKeys >= t)
                {
                    BorrowFromRightSibling(node, 0);
                }
                else
                {
                    MergeChildren(node, 0);
                }
                // Re-read child because Merge/Borrow might have changed its identity/content
                child = DiskRead(node.Kids[0]);
            }

            return DeleteMin(child);
        }

        /// <summary>
        /// Merge children at pos and pos + 1 together into a single node.
        /// The parent node is node.  The children are y and z.
        /// z will be deleted and removed from the parent.
        /// Postconditions: right child id is decommissioned (zeroed) and pushed to FreeList.
        /// </summary>
        private void MergeChildren(BNode node, int pos)
        {
            BNode y = DiskRead(node.Kids[pos]);     // Left child
            BNode z = DiskRead(node.Kids[pos + 1]); // Right child

            // 1. Pull separator from parent into Y
            y.Keys[y.NumKeys] = node.Keys[pos];

            // 2. Move all keys and kids from Z to Y
            for (int j = 0; j < z.NumKeys; j++)
            {
                y.Keys[y.NumKeys + 1 + j] = z.Keys[j];
            }

            if (!y.IsLeaf)
            {
                for (int j = 0; j <= z.NumKeys; j++)
                {
                    y.Kids[y.NumKeys + 1 + j] = z.Kids[j];
                }
            }

            y.NumKeys += 1 + z.NumKeys;

            // 3. Use the new BNode method to handle the parent's collapse
            node.RemoveKeyAndChildAt(pos, Header.Order);

            // 4. Persist
            DiskWrite(y);
            DiskWrite(node);

            // 5. Decommission Z
            ZeroOutDiskSpace(z.Id);
            FreeList.Push(z.Id);
        }

        /// <summary>
        /// Borrow from left sibling performs a single-key rotation through the parent to rebalance a thin child node without merging. 
        /// Preconditions:
        /// - `node` is the parent, `pos` is the index of the recipient child.
        /// - donor sibling at pos-1 has >= t keys (caller must ensure).
        /// Postconditions: child.NumKeys increments, sibling.NumKeys decrements, parent separator updated; all three persisted.
        /// </summary>
        private void BorrowFromLeftSibling(BNode node, int pos)
        {
            BNode child = DiskRead(node.Kids[pos]);
            BNode leftSibling = DiskRead(node.Kids[pos - 1]);

            // 1. Shift Keys and Kids in the recipient (child) to the right
            for (int i = child.NumKeys; i > 0; i--)
                child.Keys[i] = child.Keys[i - 1];

            if (!child.IsLeaf)
            {
                for (int i = child.NumKeys + 1; i > 0; i--)
                    child.Kids[i] = child.Kids[i - 1];
            }

            // 2. Perform the rotation
            child.Keys[0] = node.Keys[pos - 1]; // Parent key moves down
            node.Keys[pos - 1] = leftSibling.Keys[leftSibling.NumKeys - 1]; // Sibling key moves up

            if (!child.IsLeaf)
            {
                // Sibling's LAST child becomes recipient's FIRST child
                child.Kids[0] = leftSibling.Kids[leftSibling.NumKeys];
                leftSibling.Kids[leftSibling.NumKeys] = -1; // Clean donor pointer
            }

            child.NumKeys++;
            leftSibling.Keys[leftSibling.NumKeys - 1] = new Element { Key = -1, Data = -1 };
            leftSibling.NumKeys--;

            DiskWrite(child);
            DiskWrite(leftSibling);
            DiskWrite(node);
        }

        /// <summary>
        /// Borrow from right sibling performs a single-key rotation through the parent to rebalance a thin child node without merging. 
        /// Preconditions:
        /// - `node` is the parent, `pos` is the index of the recipient child.
        /// - donor sibling at pos+1 has >= t keys (caller must ensure).
        /// Postconditions: child.NumKeys increments, sibling.NumKeys decrements, parent separator updated; all three persisted.
        /// </summary>
        private void BorrowFromRightSibling(BNode node, int pos)
        {
            BNode child = DiskRead(node.Kids[pos]);
            BNode rightSibling = DiskRead(node.Kids[pos + 1]);

            // 1. Move parent separator key down to the end of the left child
            child.Keys[child.NumKeys] = node.Keys[pos];

            // 2. If not a leaf, move the right sibling's FIRST child to the left child's LAST slot
            if (!child.IsLeaf)
            {
                child.Kids[child.NumKeys + 1] = rightSibling.Kids[0];
            }
            child.NumKeys++;

            // 3. Move right sibling's FIRST key up to the parent
            node.Keys[pos] = rightSibling.Keys[0];

            // 4. Shift right sibling's keys left by 1
            for (int i = 0; i < rightSibling.NumKeys - 1; i++)
            {
                rightSibling.Keys[i] = rightSibling.Keys[i + 1];
            }

            // 5. Shift right sibling's child pointers left by 1
            if (!rightSibling.IsLeaf)
            {
                // FIX: Ensure we loop through all possible children slots to avoid ghosting
                for (int i = 0; i < rightSibling.NumKeys; i++)
                {
                    rightSibling.Kids[i] = rightSibling.Kids[i + 1];
                }
                // Explicitly nullify the vacated tail
                for (int i = rightSibling.NumKeys; i < Header.Order + 1; i++)
                {
                    rightSibling.Kids[i] = -1;
                }
            }

            // 6. Clean up the donor's last key slot and decrement count
            rightSibling.Keys[rightSibling.NumKeys - 1] = new Element { Key = -1, Data = -1 };
            rightSibling.NumKeys--;

            // 7. Persist all three modified nodes
            DiskWrite(node);
            DiskWrite(child);
            DiskWrite(rightSibling);
        }

        /// <summary>
        /// Recursive deletion maintaining B‑tree invariants.
        /// Before descending into a child, that child is guaranteed (by caller steps) to have >= t keys
        /// where t = ceiling((Order+1)/2). Borrow or Merge operations are performed as necessary to ensure that invariant.
        /// </summary>
        private void DeleteSafe(BNode node, Element key)
        {
            // Use ceiling math: for Order 3, t = 2. Min keys = t-1 = 1.
            int t = (Header.Order + 1) / 2;
            int pos = 0;
            // Find the first key greater than or equal to the target key
            while (pos < node.NumKeys && key.Key > node.Keys[pos].Key) pos++;

            // CASE 1: The key is found in the current node
            if (pos < node.NumKeys && key.Key == node.Keys[pos].Key)
            {
                if (node.IsLeaf)
                {
                    // Simple deletion from leaf
                    for (int j = pos; j < node.NumKeys - 1; j++)
                        node.Keys[j] = node.Keys[j + 1];

                    node.Keys[node.NumKeys - 1] = new Element { Key = -1, Data = -1 };
                    node.NumKeys--;
                    DiskWrite(node);
                }
                else
                {
                    // Internal node deletion: Replace with predecessor or successor
                    BNode y = DiskRead(node.Kids[pos]);
                    BNode z = DiskRead(node.Kids[pos + 1]);

                    if (y.NumKeys >= t)
                    {
                        // Predecessor path: find max in left child
                        node.Keys[pos] = DeleteMax(y);
                        DiskWrite(node);
                    }
                    else if (z.NumKeys >= t)
                    {
                        // Successor path: find min in right child
                        node.Keys[pos] = DeleteMin(z);
                        DiskWrite(node);
                    }
                    else
                    {
                        // Both children are thin: Merge them, then delete from the merged node
                        MergeChildren(node, pos);
                        y = DiskRead(y.Id); // Re-sync y after merge
                        DeleteSafe(y, key);
                    }
                }
            }
            // CASE 2: The key is not in this node (it's in a subtree)
            else if (!node.IsLeaf)
            {
                BNode child = DiskRead(node.Kids[pos]);

                // PRE-EMPTIVE STEP: If the child is at minimum capacity (t-1), beef it up.
                if (child.NumKeys == t - 1)
                {
                    bool borrowed = false;

                    // Try borrowing from Left Sibling
                    if (pos > 0)
                    {
                        BNode left = DiskRead(node.Kids[pos - 1]);
                        if (left.NumKeys >= t)
                        {
                            BorrowFromLeftSibling(node, pos);
                            borrowed = true;
                        }
                    }

                    // If left failed, try borrowing from Right Sibling
                    if (!borrowed && pos < node.NumKeys)
                    {
                        BNode right = DiskRead(node.Kids[pos + 1]);
                        if (right.NumKeys >= t)
                        {
                            BorrowFromRightSibling(node, pos);
                            borrowed = true;
                        }
                    }

                    // If neither sibling could spare a key, we MUST merge
                    if (!borrowed)
                    {
                        // Merge with left if we're at the end, otherwise merge with right
                        int mergeIdx = (pos < node.NumKeys) ? pos : pos - 1;
                        MergeChildren(node, mergeIdx);
                        child = DiskRead(node.Kids[mergeIdx]); // Re-sync the child pointer
                    }
                    else
                    {
                        // Re-read child to get keys moved during Borrow operations
                        child = DiskRead(node.Kids[pos]);
                    }
                }

                // Now it is guaranteed that 'child' has >= t keys
                DeleteSafe(child, key);
            }
        }

        /// ------- FREE LIST -------

        /// <summary>
        /// Persist the in-memory free list to the tail of the file and record its offset/count in the header.
        /// This approach expects single-writer semantics; concurrent writers can corrupt the tail.
        /// Caller should call SaveHeader() to persist Header.FreeListOffset/Count if needed.
        /// </summary>
        private void SaveFreeList()
        {
            if (FreeList.Count == 0) return;

            Header.FreeListCount = FreeList.Count;

            // 1. Move to the end of the file
            using (var writer = new BinaryWriter(MyFileStream, System.Text.Encoding.UTF8, true))
            {
                long offset = writer.BaseStream.Length;
                writer.BaseStream.Seek(offset, SeekOrigin.Begin);
                Header.FreeListOffset = offset;

                // 2. Write the count followed by the stack data
                // Iterate the stack to write free IDs to disk (the order is LIFO).
                foreach (int id in FreeList)
                {
                    writer.Write(id);
                }
            }
        }

        /// <summary>
        /// Load the free list from disk into memory.
        /// On open, LoadFreeList reads the free list and truncates the file tail to reclaim space.
        /// </summary>
        private void LoadFreeList()
        {
            using (var reader = new BinaryReader(MyFileStream, System.Text.Encoding.UTF8, true))
            {
                if (Header.FreeListOffset == 0 || Header.FreeListCount == 0) return;

                // 1. Jump to the list and populate the stack
                reader.BaseStream.Seek(Header.FreeListOffset, SeekOrigin.Begin);
                FreeList.Clear();
                for (int i = 0; i < Header.FreeListCount; i++)
                {
                    FreeList.Push(reader.ReadInt32());
                }

                // 2. TRUNCATE: Cut the tail off the file
                // This removes the list data from the disk but keeps it in your memory Stack.
                MyFileStream.SetLength(Header.FreeListOffset);
            }

            Header.FreeListCount = 0;
            Header.FreeListOffset = 0;
            SaveHeader();
        }

        public int GetFreeListCount()
        {
            return FreeList.Count;
        }

        /// ------- COMPACT METHODS -------

        /// <summary>
        /// Compact the file. Remove dead space.
        /// Steps:
        /// 1) enumerate reachable nodes; 2) build old->new id map; 3) write compact file; 4) atomically swap.
        /// Failure modes: if the process crashes before the swap the original file remains intact; ensure exclusive access.
        /// </summary>
        public void Compact()
        {
            if (Header.RootId == -1) return;

            string tempPath = MyFileName + ".tmp";

            // 1. Identify all reachable (live) nodes
            HashSet<int> liveNodes = new HashSet<int>();
            FindLiveNodes(Header.RootId, liveNodes);

            // 2. Create a mapping from Old ID to New ID
            // This removes gaps. If nodes 0, 2, and 5 are live, 
            // they become 0, 1, and 2 in the new file.
            var idMap = new Dictionary<int, int>();
            int nextId = 0;
            foreach (var oldId in liveNodes.OrderBy(id => id))
            {
                idMap[oldId] = nextId++;
            }

            // 3. Create the new compacted file
            using (var newTree = new BTree(tempPath, Header.Order))
            {
                foreach (var entry in idMap)
                {
                    BNode node = DiskRead(entry.Key); // Read from old
                    node.Id = entry.Value;           // Assign new ID

                    // Update child pointers to their new mapped IDs
                    if (!node.IsLeaf)
                    {
                        for (int i = 0; i <= node.NumKeys; i++)
                        {
                            if (node.Kids[i] != -1)
                                node.Kids[i] = idMap[node.Kids[i]];
                        }
                    }
                    newTree.DiskWrite(node); // Write to new
                }

                newTree.Header.RootId = idMap[Header.RootId];
                newTree.Header.NodeCount = idMap.Count;
                newTree.SaveHeader();
            }

            // 4. Swap files
            MyFileStream.Close();
            File.Delete(MyFileName);
            File.Move(tempPath, MyFileName);

            // Re-open the stream for the current instance
            MyFileStream = new FileStream(MyFileName, FileMode.Open, FileAccess.ReadWrite);
            LoadHeader();
            FreeList.Clear(); // FreeList is now empty as all space is used.
        }

        /// <summary>
        /// Get a hash set of the live nodes.
        /// </summary>
        private void FindLiveNodes(int nodeId, HashSet<int> liveNodes)
        {
            if (nodeId == -1 || !liveNodes.Add(nodeId)) return;

            BNode node = DiskRead(nodeId);
            if (!node.IsLeaf)
            {
                for (int i = 0; i <= node.NumKeys; i++)
                {
                    FindLiveNodes(node.Kids[i], liveNodes);
                }
            }
        }

        /// <summary>
        /// Get a list of zombie node IDs.
        /// </summary>
        public List<int> GetZombies()
        {
            if (Header.RootId == -1) return new List<int>();

            // 1. Get all reachable nodes
            HashSet<int> liveNodes = new HashSet<int>();
            FindLiveNodes(Header.RootId, liveNodes);

            // 2. Add all nodes currently in the FreeList
            // (We convert to HashSet for O(1) lookups)
            var freeNodes = new HashSet<int>();
            foreach (int id in FreeList)
            {
                freeNodes.Add(id);
            }

            List<int> zombies = new List<int>();

            // 3. Scan the physical file range
            for (int i = 0; i < Header.NodeCount; i++)
            {
                if (!liveNodes.Contains(i) && !freeNodes.Contains(i))
                {
                    zombies.Add(i);
                }
            }

            return zombies;
        }


        /// ------- PRINT METHODS ---------

        /// <summary>
        /// Get a list of keys.
        /// </summary>
        public List<int> GetKeys()
        {
            if (Header.RootId == -1)
            {
                return new List<int>();
            }

            BNode rootNode = DiskRead(Header.RootId);
            if (rootNode.NumKeys == 0)
            {
                return new List<int>();
            }

            List<int> list = new List<int>();
            return GetKeysRecursive(list, rootNode);
        }


        /// <summary>
        /// In-order traversal that collects all keys into `list`.
        /// This is a recursive, disk-reading traversal (DiskRead on children), so it can be I/O intensive
        /// and may hit recursion depth if the tree is extremely deep.
        /// </summary>
        private List<int> GetKeysRecursive(List<int> list, BNode node)
        {
            if (node == null || node.NumKeys == 0)
            {
                return list;
            }

            for (int i = 0; i < node.NumKeys + 1; i++)
            {
                int pos = node.Kids[i];
                if (pos >= 0)
                {
                    BNode child = DiskRead(pos);
                    GetKeysRecursive(list, child);
                }
                if (i < node.NumKeys)
                {
                    list.Add(node.Keys[i].Key);
                }
            }

            return list;
        }


        /// <summary>
        /// Write to file.
        /// </summary>
        public void WriteToFile(string fileName)
        {
            File.Delete(fileName);
            if (Header.RootId == -1) return;

            BNode rootNode = DiskRead(Header.RootId);
            if (rootNode.NumKeys == 0) return;

            using (StreamWriter sw = new StreamWriter(fileName, false))
            {
                WriteToStream(sw, rootNode);
            }
        }

        /// <summary>
        /// Write to stream.
        /// </summary>
        private void WriteToStream(StreamWriter sw, BNode node)
        {
            if (node.NumKeys == 0) return;

            for (int i = 0; i < node.NumKeys + 1; i++)
            {
                int pos = node.Kids[i];
                if (pos >= 0)
                {
                    BNode child = DiskRead(pos);
                    WriteToStream(sw, child);
                }
                if (i < node.NumKeys)
                {
                    sw.Write(node.Keys[i].Key);
                    sw.Write(", ");
                    sw.WriteLine(node.Keys[i].Data);
                }
            }
        }

        /// <summary>
        /// Level-order (BFS) traversal using a null marker to delimit levels.
        /// Note: child loop iterates physical capacity (`Header.Order`) and checks for -1 slots.
        /// Consider iterating `0..node.NumKeys` or `0..node.NumKeys+1` to reflect logical children only.
        /// </summary>
        public void PrintTreeByLevel()
        {
            if (Header.RootId == -1) return;

            BNode rootNode = DiskRead(Header.RootId);

            if (rootNode.NumKeys == 0)
            {
                Console.WriteLine("\nThe B-Tree is empty\n");
                return;
            }

            Queue<BNode> queue = new Queue<BNode>();

            // Using a null marker to distinguish levels.
            BNode marker = null;

            rootNode = DiskRead(Header.RootId); // Start with the latest root
            queue.Enqueue(rootNode);
            queue.Enqueue(marker); // Initial level marker

            while (queue.Count > 0)
            {
                BNode current = queue.Dequeue();

                if (current == marker)
                {
                    Console.WriteLine();
                    if (queue.Count > 0)
                    {
                        queue.Enqueue(marker); // Add marker for the next level
                    }
                    continue;
                }

                // Print the keys of the current node
                PrintNodeKeys(current);

                // Enqueue all possible child slots (physical capacity); each slot is checked for -1 before use.
                if (current.IsLeaf == false)
                {
                    for (int i = 0; i < Header.Order; i++)
                    {
                        if (current.Kids[i] != -1)
                        {
                            BNode child = DiskRead(current.Kids[i]);
                            queue.Enqueue(child);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Print node keys.
        /// </summary>
        /// <param name="node">BNode</param>
        private static void PrintNodeKeys(BNode node)
        {
            Console.Write("[");
            for (int i = 0; i < node.NumKeys; i++)
            {
                if (i > 0) Console.Write(", ");
                Console.Write(node.Keys[i].Key);
            }
            Console.Write("] ");
        }

        /// <summary>
        /// Print the tree breadth first (top-down, level by level).
        /// </summary>
        /// 
        private void PrintTreeSimple(int rootPageId)
        {
            if (rootPageId == -1)
            {
                Console.WriteLine("\nTree is empty.");
                return;
            }

            Queue<(int pageId, int level)> queue = new Queue<(int, int)>();
            queue.Enqueue((rootPageId, 0));
            int currentLevel = -1;
            var sb = new System.Text.StringBuilder(128);

            while (queue.Count > 0)
            {
                var (pageId, level) = queue.Dequeue();

                if (level > currentLevel)
                {
                    Console.WriteLine($"\n--- Level {level} ---");
                    currentLevel = level;
                }

                BNode node = DiskRead(pageId);

                // Build keys display without LINQ/allocation
                sb.Clear();
                for (int i = 0; i < node.NumKeys; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(node.Keys[i].Key);
                }

                Console.Write($"NodeP{pageId}: [{sb}] | ");

                if (!node.IsLeaf)
                {
                    for (int i = 0; i <= node.NumKeys; i++)
                    {
                        int childId = node.Kids[i];
                        if (childId != -1)
                        {
                            queue.Enqueue((childId, level + 1));
                        }
                    }
                }
            }
            Console.WriteLine();
        }


        /// <summary>
        /// Print the physical disk nodes.
        /// </summary>
        /// 
        public void DumpFile()
        {
            Console.WriteLine("--- PHYSICAL DISK DUMP ---");
            Console.WriteLine($"RootId: {Header.RootId}");
            var sb = new System.Text.StringBuilder(128);

            for (int i = 0; i < Header.NodeCount; i++)
            {
                try
                {
                    BNode node = DiskRead(i);
                    sb.Clear();
                    for (int j = 0; j < node.NumKeys; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        sb.Append(node.Keys[j].Key);
                    }
                    Console.WriteLine($"Page {i}: [{sb}] (IsLeaf: {node.IsLeaf})");
                }
                catch { }
            }
        }

        /// <summary>
        /// Print Pointers.
        /// </summary>
        public void PrintPointers()
        {
            Console.WriteLine("--- POINTER INTEGRITY CHECK ---");
            Console.WriteLine($"RootId: {Header.RootId}");
            for (int i = 0; i < Header.NodeCount; i++)
            {
                try
                {
                    BNode node = DiskRead(i);
                    if (node.IsLeaf) continue;

                    Console.Write($"Internal Node {i} [Keys: {node.NumKeys}]: ");
                    for (int j = 0; j <= node.NumKeys; j++)
                    {
                        Console.Write($"Kid[{j}]->Page {node.Kids[j]} | ");
                    }
                    Console.WriteLine();
                }
                catch { }
            }
        }

        /// <summary>
        /// Print the Tree.  
        /// </summary>
        public void PrintByRoot()
        {
            Console.WriteLine("--- PRINT BY ROOT ---");
            Console.WriteLine($"RootId: {Header.RootId}");
            PrintByRootRecursive(Header.RootId);
        }

        /// <summary>
        /// Print the children by depth first.
        /// </summary>
        private void PrintByRootRecursive(int nodeId, int level = 0)
        {
            if (nodeId == -1) return;
            BNode node = DiskRead(nodeId);
            string indent = new string(' ', level * 4);

            Console.Write($"{indent}NODE {node.Id} (Keys: {node.NumKeys}): ");
            for (int i = 0; i < node.NumKeys; i++) Console.Write($"{node.Keys[i].Key}, ");
            Console.WriteLine();

            if (!node.IsLeaf)
            {
                for (int i = 0; i <= node.NumKeys; i++)
                {
                    Console.WriteLine($"{indent}  Child {i} -> ID: {node.Kids[i]}");
                    PrintByRootRecursive(node.Kids[i], level + 1);
                }
            }
        }

        // ----- GHOST NODES --------

        /// <summary>
        /// Check for Ghost nodes.  
        /// </summary>
        public void CheckGhost()
        {
            CheckGhostRecursive(Header.RootId);
        }

        /// <summary>
        /// Check for ghost nodes. A ghost is an internal node (non-root) with zero keys — this is invalid.
        /// </summary>
        private void CheckGhostRecursive(int nodeId)
        {
            if (nodeId == -1) return;
            BNode node = DiskRead(nodeId);
            if (nodeId != Header.RootId && node.NumKeys == 0)
            {
                throw new Exception($"GHOST DETECTED: Node {nodeId} is an internal node with 0 keys!");
            }

            if (!node.IsLeaf)
            {
                for (int i = 0; i <= node.NumKeys; i++)
                    CheckGhostRecursive(node.Kids[i]);
            }
        }

        // -------- VALIDATION METHODS ---------

        /// <summary>
        /// Validate structural integrity: checks for cycles, ordering, boundary constraints, and minimum keys.
        /// Does NOT validate free list correctness or external file-format corruption beyond Header.Magic.
        /// </summary>
        public void ValidateIntegrity()
        {
            if (Header.RootId == -1) return;

            HashSet<int> visited = new HashSet<int>();
            CheckNodeIntegrity(Header.RootId, int.MinValue, int.MaxValue, visited);

            // Optional: Check if NodeCount matches what is actually on disk
            if (visited.Count > Header.NodeCount)
                throw new Exception(message: "Integrity Error: Reachable nodes exceed Header.NodeCount.");
        }

        /// <summary>
        /// Check Node Integrity. Check for cycles. 
        /// </summary>
        private void CheckNodeIntegrity(int nodeId, int min, int max, HashSet<int> visited)
        {
            if (nodeId == -1) return;
            if (!visited.Add(nodeId))
                throw new Exception($"Circular Reference: Node {nodeId} visited twice.");

            BNode node = DiskRead(nodeId);

            // 1. Verify Minimum Keys (except for Root)
            int t = (Header.Order + 1) / 2;
            if (nodeId != Header.RootId && node.NumKeys < t - 1)
                throw new Exception($"Underflow: Node {nodeId} has only {node.NumKeys} keys.");

            for (int i = 0; i < node.NumKeys; i++)
            {
                int currentKey = node.Keys[i].Key;

                // 2. Verify Key Ordering within node
                if (i > 0 && currentKey <= node.Keys[i - 1].Key)
                    throw new Exception($"Ordering Error: Node {nodeId} keys not sorted.");

                // 3. Verify Key is within Parent's Range
                if (currentKey < min || currentKey > max)
                    throw new Exception($"Boundary Error: Node {nodeId} Key {currentKey} outside range [{min}, {max}].");

                // 4. Recurse into children with updated boundaries
                if (!node.IsLeaf)
                {
                    int leftChildMin = (i == 0) ? min : node.Keys[i - 1].Key;
                    CheckNodeIntegrity(node.Kids[i], leftChildMin, currentKey, visited);

                    // If it's the last key, also check the rightmost child
                    if (i == node.NumKeys - 1)
                    {
                        CheckNodeIntegrity(node.Kids[i + 1], currentKey, max, visited);
                    }
                }
            }
        }

    }
}
