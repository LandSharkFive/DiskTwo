# Technical Specification: Classic B-Tree

## 1. Storage Architecture

### Fixed-Page Paging Logic
To achieve O(1) disk seeking, the engine utilizes a fixed-page size strategy. The size of every node (page) in the file is identical, determined by the `Order` defined at initialization.

* **Node Size Formula:** `(Order * 12) + 16 bytes`
* **Disk Offset Calculation:** `Offset = (NodeSize * DiskId) + 4096`
    * *Note: The first 4096 bytes are reserved for the File Header.*

### Binary Serialization
The engine uses BinaryReader and BinaryWriter for high-performance, predictable I/O.
* **Immediate Persistence:** DiskWrite operations include a regular FileStream.Flush() to ensure physical disk synchronization.
* **Data Sanitization:** When keys are deleted or pointers are shifted, vacated slots are explicitly "Nuclear Wiped" (overwritten with `-1` or `default`) to prevent stale data from causing ghost references.

## 2. Core Logic & Invariants

### Root Management
The Header stores the RootId. The architecture handles two critical root transformations:
1.  **Root Split:** When the root reaches capacity, it splits. A new node is created to act as the parent, and Header.RootId is updated.
2.  **Root Collapse:** If a deletion leaves the root with 0 keys and at least one child, the primary child is promoted to RootId. The vacated physical slot is recycled.

### Preventative Deletion 
Unlike standard B-Trees that might backtrack, Classic implements a top-down preventative deletion. As the engine descends the tree, it ensures every visited child has at least 't' keys (where 't' is the minimum degree). If a node has t-1 keys, the engine performs a Borrow or Merge operation before moving deeper.

### Space Recovery (FreeList)
To prevent infinite file growth, the system uses a FreeList.
* **Deletion:** Merged or collapsed nodes have their IDs pushed to the FreeList.
* **Insertion:** The GetNextId() method prioritizes popping an ID from the FreeList before incrementing the physical Node Count.
* **Persistence:** The FreeList is serialized to the end of the file during Close() and reloaded into memory upon construction.

## 3. Maintenance Operations

### Compaction 
An offline maintenance routine that removes fragmentation by:
1.  Identifying all reachable nodes via a traversal.
2.  Re-mapping Live nodes to contiguous IDs in a temporary file.
3.  Discarding all Zombies (orphaned nodes) and resetting the file pointer.

### Integrity Validation
The ValidateIntegrity method performs a full audit of the tree to verify:
* Key ordering (sorted ascending).
* Boundary constraints (keys stay within parent ranges).
* Minimum key requirements (underflow checks).
* Circular reference detection.

## 4. Performance Benchmarks (Order = 60)

| Record Count | Insert Time |
| :--- | :--- |
| 1,000 | 36ms |
| 10,000 | 580ms |
| 100,000 | 3.37s |
| 1,000,000 | 28s |

1. Delete Times are within 25% of Insert Times. Both Bulk Load and Compact are quick.

## 5. Terms & Definitions

### Node States & Memory Management
* **Free List:** A stack of Page IDs that have been decommissioned and are ready for immediate reuse. When a node is merged and emptied, its ID is pushed here to prevent the file size from growing unnecessarily.
* **Zombie Node:** A node that has been logically deleted or replaced but cannot be moved to the **Free List** yet because active read transactions are still accessing it. 
* **Ghost Node:** A placeholder entry or an unmaterialized node. It occupies a slot in the parent’s pointer array but contains no data, often used during massive rebalancing or concurrent splits to reserve a spot on disk.

### Structural Components
* **Separator Keys:** These are the keys stored in **Internal Nodes**. They act as "signposts" rather than data points. A separator key $K$ guides the search: all keys in the left subtree are less than $K$, and all keys in the right subtree are greater than or equal to $K$.
* **Internal Nodes:** Internal Nodes: In Classic B-Trees, internal nodes store keys, data and child pointers. 
* **Leaf Nodes:** Leaf Nodes: The bottom layer of the tree. These nodes store the data records.
* **Padded Nodes:** To facilitate seamless node splits and merges, nodes are deliberately over-sized to provide essential buffer padding. This extra capacity ensures the tree remains stable during structural rebalancing without immediate overflow.

### Key Differences
| Feature | Classic B-Tree | B+ Tree |
| :--- | :--- | :--- |
| **Data Location** | Stored in every node (Internal & Leaf) | Stored ONLY in Leaf nodes |
| **Internal Node Role** | Stores Data + Child Pointers | Stores Separator Keys + Child Pointers |
| **Search Efficiency** | Can end early at any level | Always ends at the Leaf level |
| **Sequential Scan** | Requires complex tree traversal | Simply follow Next Leaf pointers |


