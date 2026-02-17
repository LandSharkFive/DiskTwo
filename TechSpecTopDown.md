# Technical Specification: Top-Down Recursive Partitioning

## 1. The Partitioning Logic
The loader treats the sorted input as a continuous range to be mathematically partitioned into a balanced hierarchy. 

* **Pivot Selection:** The algorithm identifies "middle" elements from the sorted dataset to serve as separator keys within Internal Nodes.
* **Sub-range Delegation:** The remaining data is divided into $M+1$ contiguous sub-ranges (where $M$ is the number of keys in the current node) and passed down to the next recursive level.
* **Base Case:** When a data range fits within the target node capacity, a Leaf Node is materialized, and the recursion for that branch terminates.



## 2. Adaptive Occupancy & Density
The algorithm is tuned to maximize storage efficiency while remaining flexible for structural integrity.

* **Greedy Packing:** Nodes are saturated with keys to reach high density, frequently achieving 97% occupancy in tested scenarios.
* **Occupancy Flexibility:** To ensure structural stability across all datasets, the loader allows occupancy to fluctuate. 
* **Edge Case Handling:** In specific edge cases, density may settle lower than the standard half page minimum to maintain a valid tree.

## 3. Implementation Efficiency
* **Sequential Writes:** Nodes are written to disk using a Pre-order Traversal (Root, Left, Right), ensuring the `FileStream` moves forward and maximizes sequential I/O performance.
* **Memory Footprint:** The implementation is lightweight, as it only needs to keep the current recursion path in memory rather than the entire tree structure.
* **Direct Page Allocation:** During construction, the loader increments the node count directly from the header, only utilizing the FreeList for post-load modifications and deletions.