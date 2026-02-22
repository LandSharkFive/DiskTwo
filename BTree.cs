/*
 * ===========================================================================
 * @title: Classic B-Tree Engine v1.0
 * @author: Koivu
 * @date: 2026-02-11
 * @type: Software [Algorithm/DataStructure]
 * @description: A pure, disk-resident implementation of the Classic B-Tree 
 * architecture (internal node data storage) featuring single-pass balancing.
 * ===========================================================================
 *
 * ABSTRACT:
 * A high-performance B-Tree engine optimized for O(1) node access and robust 
 * data persistence. Unlike B+ variants, this Classic B-Tree stores data 
 * elements within internal nodes, following the original specifications 
 * defined by Bayer and McCreight.
 *
 * KEYWORDS: 
 * Classic B-Tree, Balanced Tree, Disk-Resident, Single-Pass, Top-Down Splitting, 
 * Sector Reclamation, FreeList, Binary Serialization, Persistence, O(1).
 *
 * ARCHITECTURAL SPECIFICATION:
 * - Balancing: Proactive top-down node splitting/merging (backtrack-free).
 * - Storage Management: Stack-based FreeList for sector-level reclamation.
 * - Verification: Integrated Audit suite for cycle and ghost-key detection.
 * - Memory Model: Struct-based element alignment for cache locality.
 *
 * CITATION:
 * Please cite as: Koivu, "Classic B-Tree Engine v1.0" 
 * (2026), GitHub Repo: DiskTwo.
 * ---------------------------------------------------------------------------
 * License: Distributed under the MIT License. 
 * Copyright (c) 2026. All rights reserved.
 * ---------------------------------------------------------------------------
 */


using System;
using System.Buffers;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace DiskTwo
{
    /// <summary>
    /// Provides a structured interface for BTree file operations, 
    /// ensuring safe resource management and data persistence.
    /// </summary>
    public class BTree : IDisposable
    {
        private string MyFileName { get; set; }

        private FileStream MyFileStream;

        private BinaryReader MyReader;

        private BinaryWriter MyWriter;

        private readonly HashSet<int> FreeList = new HashSet<int>();

        private const int HeaderSize = 4096;

        private const int MagicConstant = BTreeHeader.MagicConstant;

        public BTreeHeader Header;


        /// <summary>
        /// Initializes a new instance of the <see cref="BTree"/> class by opening an existing file 
        /// or creating a new one with the specified branching order.
        /// </summary>
        public BTree(string fileName, int order = 60)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("File name cannot be empty.");
            }

            if (order < 4)
            {
                throw new ArgumentException("Order must be at least 4.");
            }

            MyFileName = fileName;

            OpenStorage();

            if (MyFileStream.Length > 0)
            {
                // 2. Existing file: Trust the disk.
                LoadHeader();
                LoadFreeList();
            }
            else
            {
                // 3. New file.
                InitHeader(order);
                SaveHeader();
            }
        }

        /// <summary>
        /// Open the file stream. 
        /// </summary>
        private void OpenStorage()
        {
            const int BufferSize = 65536;

            // Close existing if they were open (safety first)
            MyWriter = null;
            MyReader = null;
            MyFileStream = null;

            // The 64KB Buffer.
            MyFileStream = new FileStream(MyFileName, FileMode.OpenOrCreate,
                                          FileAccess.ReadWrite, FileShare.None, BufferSize);

            MyReader = new BinaryReader(MyFileStream, System.Text.Encoding.UTF8, true);
            MyWriter = new BinaryWriter(MyFileStream, System.Text.Encoding.UTF8, true);
        }

        /// <summary>
        /// Initialize Header.
        /// </summary>
        /// <param name="order"></param>
        private void InitHeader(int order)
        {
            Header.Magic = BTreeHeader.MagicConstant;
            Header.Order = order;
            Header.PageSize = BNode.CalculateNodeSize(order);
            Header.RootId = -1;
            Header.NodeCount = 0;
        }

        // ------ HELPER METHODS ------


        /// <summary>
        /// Calculates the byte offset in the file for a given disk position (record index).
        /// </summary>
        private long CalculateOffset(int disk)
        {
            if (disk < 0)
                throw new ArgumentOutOfRangeException(nameof(disk), "Cannot be negative.");

            if (Header.PageSize < 64)
                throw new ArgumentException(nameof(Header.PageSize));

            return ((long)Header.PageSize * disk) + HeaderSize;
        }

        /// <summary>
        /// Closes the object and releases resources by calling the Dispose method.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Safely persists data and releases the file stream while preventing redundant cleanup.
        /// </summary>
        public void Dispose()
        {
            if (MyFileStream != null)
            {
                try
                {
                    SaveFreeList();
                    SaveHeader();
                }
                finally
                {
                    MyFileStream.Dispose();
                    MyFileStream = null;
                }
            }
            // Tell the GC we've already handled the cleanup.
            GC.SuppressFinalize(this);
        }

        // -------- DISK I/O METHODS -----------

        /// <summary>
        /// Completely wipes the B-Tree structure and truncates the underlying data file.
        /// This resets the header, clears the free list, and prepares the file for a fresh bulk load.
        /// </summary>
        /// <remarks>
        /// Warning: This operation is destructive and cannot be undone.
        /// </remarks>
        public void Clear()
        {
            // 1. Wipe the physical file
            MyFileStream.SetLength(0);
            MyFileStream.Flush();
            MyFileStream.Seek(0, SeekOrigin.Begin); // Crucial: Reset the pointer

            // 2. Reset the logical state
            Header.RootId = -1;
            Header.NodeCount = 0;

            FreeList.Clear();

            // 3. Re-initialize the Header in the file
            SaveHeader();
        }

        /// <summary>
        /// Retrieves stored data from physical storage.
        /// </summary>
        public BNode DiskRead(int disk)
        {
            if (disk < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(disk), "Cannot be negative");
            }

            BNode readNode = new BNode(Header.Order);
            long offset = CalculateOffset(disk);

            MyFileStream.Seek(offset, SeekOrigin.Begin);
            readNode.Read(MyReader);
            return readNode;
        }

        /// <summary>
        /// Write a node to disk using the fixed binary layout described in DiskRead.
        /// Ensure Header.Order and BNode layout remain compatible with previously written files.
        /// </summary>
        public void DiskWrite(BNode node)
        {
            if (node.Id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(node.Id), "Cannot be negative");
            }

            long offset = CalculateOffset(node.Id);
            MyFileStream.Seek(offset, SeekOrigin.Begin);

            node.Write(MyWriter);
            //MyFileStream.Flush();
        }

        /// <summary>
        /// Wipes a specific node's data on disk by overwriting its sector with zeros.
        /// This is typically used for security or to clean up nodes moved to a free list.
        /// </summary>
        /// 
        public void ZeroNode(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Cannot be negative");

            // 1. Rent the buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Header.PageSize);

            try
            {
                // 2. Prepare the array.
                Span<byte> zeroSpan = buffer.AsSpan(0, Header.PageSize);
                zeroSpan.Clear();

                // 3. Physical Write
                long offset = CalculateOffset(id);
                MyFileStream.Seek(offset, SeekOrigin.Begin);
                MyFileStream.Write(zeroSpan);
            }
            finally
            {
                // 4. Return the buffer
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }


        /// <summary>
        /// Synchronizes the internal file stream with the underlying storage device to ensure all changes are persisted.
        /// </summary>
        public void Commit()
        {
            if (MyFileStream != null && MyFileStream.CanWrite)
            {
                SaveHeader();
                MyFileStream.Flush();
            }
        }

        // --- HEADER METHODS ---

        /// <summary>
        /// Writes the B-Tree header to disk.
        /// </summary>
        public void SaveHeader()
        {
            // 1. Rent the 4KB buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(HeaderSize);

            try
            {
                // 2. Clear the buffer to ensure the 4KB block is zero-filled.
                Array.Clear(buffer, 0, HeaderSize);

                // 3. Wrap the buffer in a MemoryStream so the Writer can use it
                using (var ms = new MemoryStream(buffer, 0, HeaderSize))
                using (var tempWriter = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    // 4. Header writes to the buffer in memory.
                    Header.Write(tempWriter);
                }

                // 5. One single blast to the disk.
                MyFileStream.Seek(0, SeekOrigin.Begin);
                MyFileStream.Write(buffer, 0, HeaderSize);
                MyFileStream.Flush();
            }
            finally
            {
                // 6. Return the buffer
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Load the B-Tree header from disk.
        /// </summary>
        public void LoadHeader()
        {
            MyFileStream.Seek(0, SeekOrigin.Begin);
            Header = BTreeHeader.Read(MyReader);

            if (Header.Magic != MagicConstant)
                throw new InvalidDataException("Invalid File Format");

            if (Header.Order < 4)
                throw new ArgumentException("Order must be at least 4.");

            if (Header.PageSize < 64 || Header.PageSize != BNode.CalculateNodeSize(Header.Order))
                throw new ArgumentException(nameof(Header.PageSize));
        }


        // --- SEARCH METHODS ---

        /// <summary>
        /// Searches the tree for a key.
        /// </summary>
        public bool TrySearch(int key, out Element result)
        {
            result = Element.GetDefault();
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

            if (node.Leaf)
            {
                result = Element.GetDefault();
                return false;
            }

            BNode child = DiskRead(node.Kids[i]);
            return TrySearchRecursive(child, key, out result);
        }

        // ------- INSERT METHODS --------


        /// <summary>Inserts a new Element into the collection using the specified key and data.</summary>
        public void Insert(int key, int data)
        {
            Element item = new Element(key, data);
            Insert(item);
        }

        /// <summary>
        /// Inserts a new element into the tree. 
        /// </summary>
        public void Insert(Element item)
        {
            if (Header.RootId == -1)
            {
                BNode firstNode = new BNode(Header.Order) { Leaf = true, Id = GetNextId() };
                Header.RootId = firstNode.Id;
                firstNode.Keys[0] = item;
                firstNode.NumKeys = 1;
                DiskWrite(firstNode);
                SaveHeader();
                return;
            }

            BNode rootNode = DiskRead(Header.RootId);
            if (rootNode.NumKeys == Header.Order - 1)
            {
                BNode newRoot = new BNode(Header.Order, leaf: false) { Id = GetNextId() };
                newRoot.Kids[0] = Header.RootId;
                SplitChild(newRoot, 0, isRootSplit: true);
                InsertNonFull(newRoot, item);
            }
            else
            {
                InsertNonFull(rootNode, item);
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

            if (node.Leaf)
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
                    throw new ArgumentException(nameof(pos));
                }
                BNode child = DiskRead(node.Kids[pos]);

                bool splitOccurred = false;
                if (child.NumKeys == Header.Order - 1)
                {
                    // Child is full, must split it
                    SplitChild(node, pos, isRootSplit: false);

                    // Get the updated parent node after the split
                    node = DiskRead(node.Id);
                    splitOccurred = true;

                    // Determine which side of the promoted key the new key goes
                    if (key.Key > node.Keys[pos].Key)
                    {
                        pos++;
                    }
                }

                // Recursive call on the appropriate child
                child = DiskRead(node.Kids[pos]);
                InsertNonFull(child, key);
                if (splitOccurred) DiskWrite(node); // Only write if the parent's structure changed.
            }
        }

        /// <summary>
        /// Performs a B-Tree node split. This function bisects a full child node (y) 
        /// at the median index, moves the upper half into a new sibling (z), 
        /// and promotes the median key into the parent (x).
        /// </summary>
        private void SplitChild(BNode x, int pos, bool isRootSplit)
        {
            // y: The "Full" node that needs to be split
            BNode y = DiskRead(x.Kids[pos]);

            // z: The new Right Sibling that will receive y's right-hand keys/children
            BNode z = new BNode(Header.Order, leaf: y.Leaf) { Id = GetNextId() };

            // t is the 'minimum degree'. For Order 4, t = 2.
            int t = (Header.Order + 1) / 2;
            int medianIdx = t - 1; // Pivot point for the split

            // 1. DISTRIBUTE: Move the 'Right Half' (everything after the median) from y to z
            int keysToMove = y.NumKeys - t;
            z.NumKeys = keysToMove;

            for (int j = 0; j < keysToMove; j++)
            {
                z.Keys[j] = y.Keys[medianIdx + 1 + j];
                y.Keys[medianIdx + 1 + j] = Element.GetDefault(); // Wipe stale data in y
            }

            if (!y.Leaf)
            {
                // Also migrate the child pointers if this isn't a leaf split
                for (int j = 0; j <= keysToMove; j++)
                {
                    z.Kids[j] = y.Kids[medianIdx + 1 + j];
                    y.Kids[medianIdx + 1 + j] = -1; // Null out stale pointers in y
                }
            }

            // 2. TRUNCATE: y now only retains the 'Left Half'
            y.NumKeys = medianIdx;

            // 3. SHIFT PARENT: Make a "hole" in parent x at index 'pos' for the promoted key
            for (int j = x.NumKeys - 1; j >= pos; j--)
            {
                x.Keys[j + 1] = x.Keys[j];
            }
            // Shift child pointers in x to accommodate the new child z
            for (int j = x.NumKeys; j >= pos; j--)
            {
                x.Kids[j + 1] = x.Kids[j];
            }

            // 4. PROMOTE: Move y's median key up to the parent x
            x.Keys[pos] = y.Keys[medianIdx];
            y.Keys[medianIdx] = Element.GetDefault(); // Clear median from y (it now lives in x)

            x.Kids[pos + 1] = z.Id; // Link the new right sibling to the parent
            x.NumKeys++;

            // 5. PERSIST: Write changes to disk (y, z, and then x)
            DiskWrite(y);
            DiskWrite(z);
            DiskWrite(x);

            if (isRootSplit)
            {
                Header.RootId = x.Id;
                SaveHeader();
            }
        }


        /// <summary>
        /// Allocates a new unique ID by claiming the next free slot on disk.
        /// </summary>
        public int GetNextId()
        {
            // Get first item.
            using (var enumerator = FreeList.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    int nodeId = enumerator.Current;
                    FreeList.Remove(nodeId);
                    return nodeId;
                }
            }

            // Append to end of file.
            int nextPos = Header.NodeCount;
            Header.NodeCount++;
            return nextPos;
        }

        // ------ DELETE METHODS ------

        /// <summary>
        /// Removes the specified key from the B-Tree, rebalances the structure, and shrinks the tree height if the root becomes empty.
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
            if (finalRoot.NumKeys == 0 && !finalRoot.Leaf)
            {
                int oldId = Header.RootId;

                // Promote the first child to be the new King.
                Header.RootId = finalRoot.Kids[0];

                // Save the Header immediately so the Audit knows where to start.
                SaveHeader();

                // Clean up the evidence of the old root
                ZeroNode(oldId);
                FreeNode(oldId);
            }
        }

        // --- MIN AND MAX HELPERS ---
        // Min and Max are similar to Search.

        /// <summary>
        /// Traverses to the right-most leaf of the B-Tree to retrieve the element with the highest key.
        /// </summary>
        public Element? FindMax()
        {
            if (Header.RootId == -1) return null;
            BNode rootNode = DiskRead(Header.RootId);
            return FindMaxRecursive(rootNode);
        }

        /// <summary>
        /// Recursively follows the last child pointer of each node until a leaf is reached, returning its final key.
        /// </summary>
        private Element? FindMaxRecursive(BNode node)
        {
            if (node.NumKeys == 0) return null;

            if (node.Leaf)
            {
                return node.Keys[node.NumKeys - 1];
            }
            else
            {
                BNode child = DiskRead(node.Kids[node.NumKeys]);
                return FindMaxRecursive(child);
            }
        }

        /// <summary>
        /// Traverses to the left-most leaf of the B-Tree to retrieve the element with the lowest key.
        /// </summary>
        public Element? FindMin()
        {
            if (Header.RootId == -1) return null;
            BNode rootNode = DiskRead(Header.RootId);
            return FindMinRecursive(rootNode);
        }

        /// <summary>
        /// Recursively follows the first child pointer (index 0) of each node to locate the smallest element in the tree.
        /// </summary>
        private Element? FindMinRecursive(BNode node)
        {
            if (node.NumKeys == 0) return null;

            if (node.Leaf)
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

            if (node.Leaf)
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
            if (node.Leaf)
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
        /// Merges two sibling nodes by pulling the separator key from the parent into the left child, 
        /// appending all contents from the right child, and decommissioning the now-redundant right node.
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

            if (!y.Leaf)
            {
                for (int j = 0; j <= z.NumKeys; j++)
                {
                    y.Kids[y.NumKeys + 1 + j] = z.Kids[j];
                }
            }

            y.NumKeys += 1 + z.NumKeys;

            // 3. Use the new BNode method to handle the parent's collapse
            node.RemoveKeyAndChildAt(pos);

            // 4. Persist
            DiskWrite(y);
            DiskWrite(node);

            // 5. Decommission Z
            ZeroNode(z.Id);
            FreeNode(z.Id);
        }

        /// <summary>
        /// Performs a rightward rotation to rebalance a child node by moving a key from the parent down into the child,
        /// and promoting the largest key from the left sibling up into the parent.
        /// </summary>
        private void BorrowFromLeftSibling(BNode node, int pos)
        {
            BNode child = DiskRead(node.Kids[pos]);
            BNode leftSibling = DiskRead(node.Kids[pos - 1]);

            // 1. Shift Keys and Kids in the recipient (child) to the right
            for (int i = child.NumKeys; i > 0; i--)
                child.Keys[i] = child.Keys[i - 1];

            if (!child.Leaf)
            {
                for (int i = child.NumKeys + 1; i > 0; i--)
                    child.Kids[i] = child.Kids[i - 1];
            }

            // 2. Perform the rotation
            child.Keys[0] = node.Keys[pos - 1]; // Parent key moves down
            node.Keys[pos - 1] = leftSibling.Keys[leftSibling.NumKeys - 1]; // Sibling key moves up

            if (!child.Leaf)
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
        /// Performs a leftward rotation to rebalance a child node by moving a key from the parent down to the end of the child, 
        /// and promoting the smallest key from the right sibling up into the parent.
        /// </summary>
        private void BorrowFromRightSibling(BNode node, int pos)
        {
            BNode child = DiskRead(node.Kids[pos]);
            BNode rightSibling = DiskRead(node.Kids[pos + 1]);

            // 1. Move parent separator key down to the end of the left child
            child.Keys[child.NumKeys] = node.Keys[pos];

            // 2. If not a leaf, move the right sibling's FIRST child to the left child's LAST slot
            if (!child.Leaf)
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
            if (!rightSibling.Leaf)
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
        /// Performs a top-down, single-pass recursive deletion. 
        /// </summary>
        /// <remarks>
        /// This method proactively rebalances the tree by ensuring every child node visited has at least 't' keys 
        /// (minimum degree) before recursion. By performing rotations (Borrow) or Merges during the descent, 
        /// it guarantees that a deletion can be completed in a single trip to the leaf without backtracking.
        /// </remarks>
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
                if (node.Leaf)
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
            else if (!node.Leaf)
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
        /// Reclaims a decommissioned node's ID by adding it to the pool of available addresses for future allocation.
        /// </summary>
        public void FreeNode(int id)
        {
            if (id < 0) return;
            FreeList.Add(id);
        }

        /// <summary>
        /// Returns the total number of recycled node slots currently available for reuse.
        /// </summary>
        public int GetFreeListCount()
        {
            return FreeList.Count;
        }


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
            long offset = MyWriter.BaseStream.Length;
            MyWriter.BaseStream.Seek(offset, SeekOrigin.Begin);
            Header.FreeListOffset = offset;

            // 2. Write the count followed by the stack data
            // Iterate the stack to write free IDs to disk (the order is LIFO).
            foreach (int id in FreeList)
            {
                MyWriter.Write(id);
            }
        }

        /// <summary>
        /// Load the free list from disk into memory.
        /// On open, LoadFreeList reads the free list and truncates the file tail to reclaim space.
        /// </summary>
        private void LoadFreeList()
        {
            if (Header.FreeListOffset == 0 || Header.FreeListCount == 0) return;

            // 1. Jump to the list and populate the stack.
            MyReader.BaseStream.Seek(Header.FreeListOffset, SeekOrigin.Begin);
            FreeList.Clear();
            for (int i = 0; i < Header.FreeListCount; i++)
            {
                FreeList.Add(MyReader.ReadInt32());
            }

            // 2. TRUNCATE: Cut the tail off the file.
            // This removes the list data from the disk but keeps it in your memory Stack.
            MyFileStream.SetLength(Header.FreeListOffset);

            Header.FreeListCount = 0;
            Header.FreeListOffset = 0;
            SaveHeader();
        }

        public void PrintFreeList()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("FreeList: ");
            int count = 0;
            foreach (var id in FreeList)
            {
                count++;
                if (count > 50) break;
                sb.Append(id);
                sb.Append(" ");
            }
            Console.WriteLine(sb);
        }

        /// ------- COMPACT METHODS -------

        /// <summary>
        /// Reorganizes the physical storage of the B-Tree to eliminate fragmentation and reclaim disk space.
        /// </summary>
        /// <remarks>
        /// This method performs a "Copy On Write" style compaction. It uses a bitmask to 
        /// efficiently identify live nodes, re-maps their IDs to a contiguous sequence, 
        /// and streams them to a temporary file. This process eliminates gaps (fragmentation), 
        /// clears the FreeList, and ensures data integrity via an atomic file swap.
        /// </remarks>
        public void Compact()
        {
            if (Header.RootId == -1) return;
            if (Header.NodeCount == 0) return;

            string tempPath = MyFileName + ".tmp";

            // 1. Identify all reachable (live) nodes
            BitArray liveNodes = new BitArray(Header.NodeCount);
            FindLiveNodes(Header.RootId, liveNodes);

            // 2. Create a mapping from Old ID to New ID
            // This removes gaps. If nodes 0, 2, and 5 are live, 
            // they become 0, 1, and 2 in the new file.
            var idMap = new Dictionary<int, int>();
            int nextId = 0;
            for (int i = 0; i < liveNodes.Count; i++)
            {
                if (liveNodes.Get(i))
                {
                    idMap[i] = nextId++;
                }
            }

            // 3. Create the new compacted file
            using (var newTree = new BTree(tempPath, Header.Order))
            {
                foreach (var entry in idMap)
                {
                    BNode node = DiskRead(entry.Key); // Read from old ID.
                    node.Id = entry.Value;           // Assign new ID.

                    // Update child pointers to their new mapped IDs.
                    if (!node.Leaf)
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

            // 4. SHUT DOWN THE OLD STREAM
            MyReader.Dispose();
            MyWriter.Dispose(); // Dispose the writer first
            MyFileStream.Dispose();

            MyReader = null;
            MyWriter = null;
            MyFileStream = null;

            // 5. Swap files
            string backupPath = MyFileName + ".bak";
            File.Replace(tempPath, MyFileName, backupPath);
            File.Delete(backupPath);

            // 7. Re-open the stream for the current instance.
            OpenStorage();
            LoadHeader();
            FreeList.Clear(); // FreeList is now empty as all space is used.
        }


        /// <summary>
        /// Check for valid node offset to prevent EndOfStreamException.
        /// </summary>
        private bool IsValidNodeOffset(int nodeId)
        {
            if (nodeId < 0) return false;

            // Calculate expected offset: HeaderSize + (nodeId * NodeSize)
            long offset = HeaderSize + (long)nodeId * Header.PageSize;
            return offset >= 0 && offset < MyFileStream.Length;
        }

        /// <summary>
        /// Recursively traverses the B-Tree to identify all reachable nodes, populating a set of active node IDs.
        /// </summary>
        /// <remarks>
        /// This acts as a "mark" phase for compaction. It performs deep validation on node offsets 
        /// and handles cycle detection (via BitArray) to ensure only structurally sound, 
        /// accessible data is preserved in the storage file.
        /// </remarks>
        private void FindLiveNodes(int nodeId, BitArray liveNodes)
        {
            // 1. Boundary Check: Prevent EndOfStreamException
            if (nodeId < 0 || !IsValidNodeOffset(nodeId) || nodeId > liveNodes.Count)
                return;

            // 2. Have we already been here before?
            if (liveNodes.Get(nodeId))
            {
                throw new ArgumentException(nameof(nodeId), "Cycle Detected");
            }

            // 3. Mark the node.
            liveNodes.Set(nodeId, true);
            BNode node = DiskRead(nodeId);

            if (!node.Leaf)
            {
                // 4. Visit each child.
                for (int i = 0; i <= node.NumKeys; i++)
                {
                    int childId = node.Kids[i];
                    if (childId != -1)
                    {
                        FindLiveNodes(childId, liveNodes);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a list of node IDs that are leaked (orphaned) within the physical file.
        /// </summary>
        /// <remarks>
        /// Performs an audit by marking all reachable nodes and all nodes in the FreeList 
        /// into a bitmask. Any bit remaining unset represents a 'Zombie'—a node that is 
        /// neither part of the tree nor available for reuse, indicating a leak or corruption.
        /// </remarks>
        public void GetZombies(List<int> zombies)
        {
            if (Header.RootId == -1) return;
            if (Header.NodeCount == 0) return;

            // 1. Mark all reachable nodes.
            BitArray accountedFor = new BitArray(Header.NodeCount);
            FindLiveNodes(Header.RootId, accountedFor);

            // 2. Mark all free nodes in the same BitArray.
            foreach (int id in FreeList)
            {
                accountedFor.Set(id, true);
            }

            // 3. Any index still false is a zombie.
            for (int i = 0; i < Header.NodeCount; i++)
            {
                if (!accountedFor.Get(i))
                {
                    if (!zombies.Contains(i))
                        zombies.Add(i);
                }
            }
        }

        /// <summary>
        /// Returns the total count of leaked (orphaned) nodes.
        /// </summary>
        /// <remarks>
        /// Useful for health checks and determining if a Compact operation is necessary. 
        /// Utilizes the same bitmask audit logic as GetZombies.
        /// </remarks>
        public int CountZombies()
        {
            if (Header.RootId == -1) return 0;
            if (Header.NodeCount == 0) return 0;

            // 1. Mark all reachable nodes.
            BitArray accountedFor = new BitArray(Header.NodeCount);
            FindLiveNodes(Header.RootId, accountedFor);

            // 2. Mark all free nodes in the same BitArray.
            foreach (int id in FreeList)
            {
                accountedFor.Set(id, true);
            }

            int count = 0;

            // 3. All nodes that are false are zombie nodes.
            for (int i = 0; i < Header.NodeCount; i++)
            {
                if (!accountedFor.Get(i))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Scans for "Zombie" nodes—allocated nodes that are unreachable from the root.
        /// Reclaims orphans by zeroing their data and returning them to the FreeList.
        /// Effectively acts as a garbage collector for the tree structure after 
        /// intensive operations like Bulk Loading.
        /// </summary>
        public void ReclaimOrphans()
        {
            List<int> zombies = new List<int>();
            GetZombies(zombies);
            foreach (var id in zombies)
            {
                ZeroNode(id);
                FreeNode(id);
            }
        }


        /// ------- PRINT METHODS ---------


        /// <summary>
        /// A high-performance diagnostic tool that recursively calculates the total key population.
        /// </summary>
        /// <remarks>
        /// Optimized for use in unit tests and health checks, this method performs a structural 
        /// audit during traversal. It is significantly faster than a full data export, 
        /// especially when paired with a node cache.
        /// </remarks>
        public int CountKeys(int nodeId, HashSet<int> visited = null, int depth = 0)
        {
            // 1. Immediate exit for invalid IDs.
            if (nodeId < 0) return 0;

            // 2. Prevent Stack Overflow - even 32 is massive for a healthy B-Tree.
            if (depth > 64) return 0;

            // 3. HashSet handles growth better than BitArray for sparse/corrupt IDs
            visited ??= new HashSet<int>();
            if (!visited.Add(nodeId))
            {
                Console.WriteLine($"Cycle Detected: Node {nodeId}");
                return 0;
            }

            // 4. Manual validation instead of try-catch.
            BNode node = DiskRead(nodeId);

            // Check if DiskRead failed or returned a null or broken object.
            if (node == null || node.Keys == null || node.Kids == null) return 0;

            // 5. Hard boundary check against the 'Order' defined in the class.
            // If NumKeys is garbage, it could cause an IndexOutOfRange in the loop below.
            if (node.NumKeys < 0 || node.NumKeys > node.Order)
            {
                return 0;
            }

            int count = node.NumKeys;

            // 6. Recurse only if it's an internal node and children are within physical array bounds.
            if (!node.Leaf)
            {
                // The Kids array size is Order + 1
                int maxPossibleChildIndex = Math.Min(node.NumKeys, node.Order);

                for (int i = 0; i <= maxPossibleChildIndex; i++)
                {
                    int childId = node.Kids[i];
                    if (childId != -1 && childId != nodeId) // Basic self-reference check
                    {
                        count += CountKeys(childId, visited, depth + 1);
                    }
                }
            }

            return count;
        }


        /// <summary>
        /// Flattens the current B-Tree structure into a list of keys via a recursive traversal.
        /// </summary>
        /// <remarks>
        /// Under normal conditions, this returns keys in ascending order. If the output is unsorted, 
        /// it indicates a structural corruption, such as a failed rebalance, an incorrect split, 
        /// or a violation of the B-Tree search invariants.
        /// </remarks>
        public List<int> GetKeys()
        {
            if (Header.RootId == -1) return new List<int>();

            BNode rootNode = DiskRead(Header.RootId);
            // If root exists but is empty, return empty list (legit for new trees).
            if (rootNode.NumKeys == 0) return new List<int>();

            List<int> list = new List<int>();
            GetKeysRecursive(list, rootNode);
            return list;
        }


        /// <summary>
        /// Level 2 Diagnostic: Flattens the tree into a list of keys to verify sort-order and reachability.
        /// </summary>
        /// <remarks>
        /// This method is invoked only when Level 1 diagnostics (CountKeys) indicate a potential 
        /// structural failure. By performing a full in-order traversal, it allows for the 
        /// identification of "lost" keys (orphaned branches) or search-invariant violations (unsorted keys).
        /// </remarks>
        private void GetKeysRecursive(List<int> list, BNode node)
        {
            for (int i = 0; i < node.NumKeys; i++)
            {
                // 1. Visit the child to the left of the current key.
                if (!node.Leaf && node.Kids[i] != -1)
                {
                    GetKeysRecursive(list, DiskRead(node.Kids[i]));
                }

                // 2. Add key to the list.
                list.Add(node.Keys[i].Key);
            }

            // 3. Visit the right-most child.
            if (!node.Leaf && node.Kids[node.NumKeys] != -1)
            {
                GetKeysRecursive(list, DiskRead(node.Kids[node.NumKeys]));
            }
        }

        /// <summary>
        /// Efficiently retrieves all unique elements from the tree using a HashSet for deduplication.
        /// </summary>
        public List<Element> GetElements()
        {
            // We use a HashSet for O(1) lookup speed during the crawl
            var uniqueSet = new HashSet<Element>();

            if (Header.RootId == -1)
                return new List<Element>();

            GetElementsRecursive(Header.RootId, uniqueSet, 0);

            // Convert back to list for the return type
            return uniqueSet.ToList();
        }

        private void GetElementsRecursive(int nodeId, HashSet<Element> result, int depth)
        {
            // 1. Validate Node ID
            if (nodeId == -1) return;

            // 2. Prevent infinite recursion (Safety check for corrupted disk pointers)
            if (depth > 64)
                throw new ArgumentException(nameof(depth), "Cycle Detected");

            // 3. Attempt Disk Read
            BNode node = DiskRead(nodeId);
            if (node == null) return;

            // 4. Process Leaf Nodes
            if (node.Leaf)
            {
                for (int i = 0; i < node.NumKeys; i++)
                {
                    result.Add(node.Keys[i]); // HashSet handles the "Contains" check automatically
                }
            }
            else
            {
                // 5. Process Internal Nodes (In-Order Traversal)
                for (int i = 0; i <= node.NumKeys; i++)
                {
                    // Visit Child branch
                    if (node.Kids != null && i < node.Kids.Length)
                    {
                        GetElementsRecursive(node.Kids[i], result, depth + 1);
                    }

                    // Visit the Key at this index
                    if (i < node.NumKeys)
                    {
                        result.Add(node.Keys[i]);
                    }
                }
            }
        }


        /// <summary>
        /// Extracts all tree data into a flat CSV format, acting as a "Recovery Dump."
        /// </summary>
        /// <remarks>
        /// Designed to facilitate a "Dump and Reload" strategy for repairing corrupted or 
        /// severely unbalanced trees. The output file preserves Key/Data pairs in sorted order, 
        /// providing a clean source for BulkLoad operations to rebuild a healthy structure.
        /// </remarks>
        /// 
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
        /// Recursively traverses the B-Tree in-order and serializes each key-data pair to the provided stream.
        /// </summary>
        /// <remarks>
        /// Following the B-Tree search invariant, this visits the left subtree, then the separator key, 
        /// then the right subtree, resulting in a physically sorted output stream.
        /// </remarks>
        private void WriteToStream(StreamWriter sw, BNode node)
        {
            if (node.NumKeys == 0) return;

            if (node.Leaf)
            {
                for (int i = 0; i < node.NumKeys; i++)
                {
                    var searchKey = node.Keys[i];
                    sw.WriteLine($"{searchKey.Key}, {searchKey.Data}");
                }
            }
            else
            {
                for (int i = 0; i <= node.NumKeys; i++)
                {
                    int k = node.Kids[i];
                    if (k >= 0)
                    {
                        var child = DiskRead(node.Kids[i]);
                        WriteToStream(sw, child);
                    }

                    if (i < node.NumKeys)
                    {
                        var searchKey = node.Keys[i];
                        sw.WriteLine($"{searchKey.Key}, {searchKey.Data}");
                    }
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
                Console.WriteLine("\nThe B-Tree is empty.");
            }

            Queue<BNode> queue = new Queue<BNode>();

            // Using a null marker to distinguish levels.
            BNode marker = null;

            rootNode = DiskRead(Header.RootId); // Start with the latest root.
            queue.Enqueue(rootNode);
            queue.Enqueue(marker); // Initial level marker.

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
                if (current.Leaf == false)
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
            Console.WriteLine();
        }

        /// <summary>
        /// Print node keys.
        /// </summary>
        private static void PrintNodeKeys(BNode node)
        {
            StringBuilder sb = new StringBuilder(128);
            sb.Append("[");
            for (int i = 0; i < node.NumKeys; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(node.Keys[i].Key);
            }
            sb.Append("] ");
            Console.Write(sb);
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

                if (!node.Leaf)
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
                    Console.WriteLine($"Page {i}: [{sb}] (Leaf: {node.Leaf})");
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
                    if (node.Leaf) continue;

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
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < node.NumKeys; i++)
            {
                sb.Append(node.Keys[i].Key);
                sb.Append(" ");
            }
            Console.WriteLine(sb);

            if (!node.Leaf)
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
            if (Header.NodeCount == 0) return;
            BitArray visited = new BitArray(Header.NodeCount);
            CheckGhostRecursive(Header.RootId, visited);
        }

        /// <summary>
        /// Check for ghost nodes. A ghost is an internal node (non-root) with zero keys — this is invalid.
        /// </summary>
        private void CheckGhostRecursive(int nodeId, BitArray visited = null)
        {
            if (nodeId <= 0) return;
            if (Header.NodeCount == 0) return;

            if (visited == null)
            {
                visited = new BitArray(Header.NodeCount);
            }

            // Check for cycles.
            if (visited.Get(nodeId))
            {
                throw new ArgumentException(nameof(nodeId), "Cycle Detected");
            }

            BNode node = DiskRead(nodeId);
            visited.Set(nodeId, true);
            if (nodeId != Header.RootId && node.NumKeys == 0)
            {
                throw new ArgumentException(nameof(nodeId), "Ghost Detected");
            }

            if (!node.Leaf)
            {
                if (node.NumKeys > Header.Order)
                {
                    throw new ArgumentException(nameof(node.NumKeys));
                }

                for (int i = 0; i <= node.NumKeys; i++)
                {
                    if (node.Kids[i] != -1)
                    {
                        CheckGhostRecursive(node.Kids[i], visited);
                    }
                }
            }
        }

        /// <summary>
        /// Scans the entire physical file to count "Ghost" nodes (nodes with zero keys).
        /// </summary>
        /// <remarks>
        /// Unlike CountZombies, this is an I/O-intensive brute-force scan that inspects 
        /// every node's content. It identifies nodes that are physically present but logically empty; 
        /// these may be valid empty nodes, newly allocated space, or remnants of a failed deletion.
        /// </remarks>
        public int CountGhost()
        {
            int count = 0;
            for (int i = 0; i < Header.NodeCount; i++)
            {
                try
                {
                    BNode node = DiskRead(i);
                    if (node.NumKeys == 0)
                    {
                        count++;
                    }
                }
                catch { }
            }
            return count;
        }

        // -------- VALIDATION METHODS ---------

        /// <summary>
        /// Validate structural integrity: checks for cycles, ordering, boundary constraints and minimum keys.
        /// Does NOT validate free list correctness or external file-format corruption beyond Header.Magic.
        /// </summary>
        public void ValidateIntegrity()
        {
            if (Header.RootId == -1) return;
            if (Header.NodeCount == 0) return;

            BitArray visited = new BitArray(Header.NodeCount);
            CheckNodeIntegrity(Header.RootId, int.MinValue, int.MaxValue, visited);

            // Optional: Check if NodeCount matches what is actually on disk
            if (visited.Count > Header.NodeCount)
                throw new Exception(message: "Reachable nodes cannot exceed NodeCount");
        }

        /// <summary>
        /// Check Node Integrity. Check for cycles. 
        /// </summary>
        private void CheckNodeIntegrity(int nodeId, int min, int max, BitArray visited)
        {
            if (nodeId == -1) return;
            if (nodeId > visited.Count) return;

            if (visited.Get(nodeId))
                throw new ArgumentException(nameof(nodeId), "Cycle Detected");

            visited.Set(nodeId, true);
            BNode node = DiskRead(nodeId);

            // 1. Verify Minimum Keys (except the Root).
            int t = (Header.Order + 1) / 2;
            if (nodeId != Header.RootId && node.NumKeys < t - 1)
                throw new ArgumentException(nameof(nodeId), "Underflow");

            for (int i = 0; i < node.NumKeys; i++)
            {
                int currentKey = node.Keys[i].Key;

                // 2. Verify Key Ordering within node
                if (i > 0 && currentKey <= node.Keys[i - 1].Key)
                    throw new ArgumentException(nameof(nodeId), "Must be sorted");

                // 3. Verify Key is within Parent's Range
                if (currentKey < min || currentKey > max)
                    throw new ArgumentException(nameof(currentKey));

                // 4. Recurse into children with updated boundaries.
                if (!node.Leaf)
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

        // -------- DIAGNOSTIC METHODS ---------

        public int GetHeight()
        {
            int height = 0;
            int currentId = this.Header.RootId;

            while (currentId != -1) // Assuming -1 or a similar null-pointer.
            {
                height++;
                var node = this.DiskRead(currentId);
                if (node.Leaf) break;

                currentId = node.Kids[0]; // Always follow the first child.
            }
            return height;
        }

        // -------- AUDIT METHODS ---------

        /// <summary>
        /// A data transfer object containing a snapshot of the B-Tree's physical and logical health.
        /// </summary>
        public class TreeHealthReport
        {
            public int Height { get; set; }
            public int ZombieCount { get; set; }
            public int GhostCount { get; set; }
            public int ReachableNodes { get; set; }
            public int TotalKeys { get; set; }
            public double AverageDensity { get; set; }
        }

        /// <summary>
        /// Executes a comprehensive deep-scan of the B-Tree to calculate storage efficiency and structural integrity.
        /// </summary>
        /// <returns>A TreeHealthReport detailing fragmentation, leaks, and tree geometry.</returns>
        /// <remarks>
        /// This method combines a recursive tree walk with a physical file scan. It identifies "Zombies" 
        /// (unaccounted space) and "Ghosts" (dangling pointers) while calculating the utilization density 
        /// across all reachable nodes.
        /// </remarks>
        public TreeHealthReport PerformFullAudit()
        {
            var report = new TreeHealthReport();
            if (Header.RootId == -1 || Header.NodeCount == 0) return report;

            BitArray accountedFor = new BitArray(Header.NodeCount);

            // We can calculate height and find ghosts during the live-node crawl.
            report.Height = AuditRecursive(Header.RootId, 1, accountedFor, report);

            // Average Density Calculation.
            if (report.ReachableNodes > 0)
            {
                // Max capacity per node is Order - 1
                double totalCapacity = (double)report.ReachableNodes * (Header.Order - 1);
                report.AverageDensity = (report.TotalKeys / totalCapacity) * 100.0;
            }

            // Count Zombies (Pages in file that were never reached).
            int freeListCount = 0;
            foreach (int id in FreeList)
            {
                if (id >= 0 && id < Header.NodeCount)
                {
                    accountedFor.Set(id, true);
                    freeListCount++;
                }
            }

            for (int i = 0; i < Header.NodeCount; i++)
            {
                if (!accountedFor.Get(i)) report.ZombieCount++;
            }

            return report;
        }

        /// <summary>
        /// The recursive engine for PerformFullAudit. Traverses the tree to discover 
        /// live nodes and calculate height.
        /// </summary>
        /// <returns>The maximum depth reached by this subtree.</returns>
        private int AuditRecursive(int id, int currentDepth, BitArray accountedFor, TreeHealthReport report)
        {
            // Ghost Check: Pointer points outside the physical file.
            if (id < 0 || id >= Header.NodeCount)
            {
                report.GhostCount++;
                return currentDepth;
            }

            if (id > accountedFor.Count) return 0;

            // Circular Reference Check: Prevents infinite recursion.
            if (accountedFor.Get(id)) return currentDepth;

            accountedFor.Set(id, true);
            report.ReachableNodes++;

            var node = DiskRead(id);
            report.TotalKeys += node.NumKeys;
            if (node.Leaf) return currentDepth;

            int maxSubtreeHeight = currentDepth;
            for (int i = 0; i <= node.NumKeys; i++)
            {
                int childHeight = AuditRecursive(node.Kids[i], currentDepth + 1, accountedFor, report);
                if (childHeight > maxSubtreeHeight) maxSubtreeHeight = childHeight;
            }

            return maxSubtreeHeight;
        }

    }
}
