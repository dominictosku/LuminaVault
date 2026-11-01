# 🏠 Home Inventory & 3D Planner

LuminaVault provides an intuitive spatial inventory system to catalog everything you own, track values for net worth calculations, attach documents and photos, and visualize storage locations in an interactive 3D scene.

---

## 1. Spatial Hierarchy

Items are organized in a 5-level real-world hierarchy:

```
House
 └── Room
      └── Furniture (Cabinet, Shelf, Wardrobe, Desk, Bed, etc.)
           └── Container (Drawer, Box, Bin, Shelf level)
                └── Item
```

- **House**: Top-level dwelling or property (e.g., "Main Apartment", "Vacation Home").
- **Room**: Physical room with dimensions (`width`, `depth`, `height`) and color coding.
- **Furniture**: Storage or furniture piece placed within a room with 3D coordinates (`x, y, z`, `width, depth, height`, `rotationY`) and furniture kind.
- **Container**: Optional sub-division inside furniture (e.g. "Top Drawer", "Middle Shelf", "Toolbox").
- **Item**: Asset with properties, value, quantity, attachments, and optional 3D model. Items can also be placed directly in a room as "loose items" without assigned furniture.

---

## 2. Interactive 3D Planner

The 3D planner (`/planner`) renders your home environment using **Three.js** and **GSAP**:

- **Room Navigation**: Click on any room in the floor plan to smoothly zoom the camera inside.
- **Furniture Inspection**: Click furniture to zoom in and inspect its openable elements (drawers sliding out, cabinet doors swinging open).
- **Floating Item Markers**: Items stored in furniture or loose in a room float as glowing animated orbs above their locations. Clicking an orb triggers a particle burst and displays the item's details card.
- **Procedural Geometry**: Cabinets, desks, shelves, wardrobes, beds, sofas, and boxes are generated procedurally with natural dimensions and materials.
- **Custom Furniture Models**: Replace procedural geometry with high-fidelity glTF models by dropping files into the backend working directory:
  ```
  LuminaVault-Backend/models/Cabinet.glb
  LuminaVault-Backend/models/Sofa.glb
  ...
  ```
- **Per-Item 3D Models**: Users can upload a custom `.glb` or `.gltf` model for any item (up to 50 MB). When uploaded, the custom 3D model replaces the floating orb in the scene.

---

## 3. Lithium Battery Tracking & Maintenance

Rechargeable lithium-ion and lithium-polymer batteries degrade permanently if left discharged in storage for extended periods. LuminaVault includes built-in battery maintenance monitoring:

### Configuration
On any item (e.g., power tools, cameras, flashlights, tablets, emergency electronics):
- **Has Lithium Battery**: Checkbox toggle to enable tracking.
- **Charge Interval (Months)**: Maintenance reminder frequency (default: **3 months**).
- **Last Charged Date**: Date of the last full charge (falls back to purchase date or item creation date if empty).

### Visual Attention & Status
Items needing battery attention stand out immediately:
- **Card Highlight**: Prominent amber glowing border, warm tint, and warning ring.
- **Attention Badge**: `⚡ Needs charge` badge with pulse animation and overdue days counter.
- **Healthy Badge**: `⚡ Battery OK` badge for up-to-date items.
- **Filter Pills**: One-click filter pill on the Assets page to instantly view only items requiring a recharge.
- **3D Planner Tint**: In the 3D scene, orbs representing items needing battery attention glow in amber (`#f59e0b`) with boosted emissive intensity.

### Quick Actions & Alerts
- **One-Click Charge Button**: Mark an item as charged directly from the asset list card or the detail form. This automatically updates `LastChargedAt` to today and dismisses pending alerts.
- **Automated Alerts**: The background notification scanner flags overdue batteries as `Warning` (or `Critical` if overdue by >14 days) with links directly to the asset.

---

## 4. Photos & Documents

- **Photo Gallery**: Upload multiple photos per item with thumbnails and full-size modal viewer.
- **Document Attachments**: Store PDF manuals, warranty certificates, purchase receipts, and invoices directly alongside the item.
- **Protected Media Delivery**: All media endpoints are authenticated and protected against hotlinking or unauthenticated downloads.
