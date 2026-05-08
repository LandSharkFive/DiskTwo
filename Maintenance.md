# Maintenance

## 1. Offline Compaction
As deletions occur, the file may develop holes. While the FreeList tracks these, a heavily modified tree might have a physical file size much larger than its logical data.

**How it works:**
1. Creates a temporary file.
2. Performs a Breadth-First Search (BFS) starting from the RootId.
3. Re-maps every live node to a new, contiguous ID.
4. Swaps the old file with the new, optimized file.

## 2. Bulk Loading
To avoid the overhead of 'N' separate Insert operations,  use the BulkLoad method.

* **Sort First:** Ensure your Element list is sorted by Key ascending.
* **Leaf-Up Construction:** This method builds the tree from the bottom up, filling pages to maximum capacity (or a configurable fill factor) to minimize tree height.

## 3. The FreeList Strategy
The FreeList is a stack of integers stored at the end of the file. 
* **Push:** When a node becomes empty due to a merge, its ID is added to the Free List for future allocation.
* **Pop:** When Insert or Split needs a new node ID, it checks the stack before incrementing Header.NodeCount.