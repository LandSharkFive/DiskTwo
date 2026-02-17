# Storage Density Analysis: B-Tree Bulk Loader

## 1. Theoretical Framework
In a B-Tree of **Order $M$**, the maximum number of keys per node is $M - 1$. 
Standard incremental insertions typically result in a density of **ln(2) ≈ 69.3%**. 
The "Lazy Kidnapper" Bulk Load bypasses this by packing nodes to the maximum theoretical limit before "kidnapping" the next key as a separator.

## 2. Density Formula
The efficiency of the tree is measured by comparing utilized key slots against total allocated space:

$$Density = \left( \frac{\text{Total Keys}}{\text{Total Pages} \times (Order - 1)} \right) \times 100$$

## 3. Example 
| Parameter | Value |
| :--- | :--- |
| **Order** | 4 |
| **Max Keys Per Page** | 3 |
| **Total Pages in File** | 17 |
| **Total Capacity** | 51 Slots |
| **Actual Keys Stored** | 50 |V
| **Density** | **98%** |

## 4. Read and Write Efficency
- **Reduced IOPS:** Higher density ensures that more keys are retrieved per single disk read, minimizing the total input/output operations required for large scans.
- **Search Depth:** By maximizing node capacity, the tree height remains as shallow as possible. In this test, 50 keys are reachable in only 3 hops, even with an Order as small as 4.
- **Minimal Disk:** Fragmentation: With a 98% fill rate, the physical file contains almost zero "dead space," making the index highly compact for archival or transport.

## 5. Implementation Note
The density is controlled via the LeafFactor variable. 
- **1.0 Factor:** Maximum density (98%). Best for read-only archives.
- **0.7 Factor:** Balanced density (70%). Best for trees expecting frequent future Insert operations.
