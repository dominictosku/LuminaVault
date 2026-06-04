import type { Item } from '../../core/models';

/// Scene-graph tree-walks. The planner tags meshes (and their glTF/procedural
/// descendants) with `furnitureId`/`item` in userData, then walks up from a raycast
/// hit to find the owning entity. Typed structurally (not against THREE.Object3D) so
/// the helpers stay dependency-free and unit-testable — any THREE.Object3D satisfies
/// the shape.
export interface SceneNode {
  userData?: Record<string, unknown>;
  parent?: SceneNode | null;
}

export function findFurnitureIdInAncestors(o: SceneNode | null | undefined): number | null {
  let cur: SceneNode | null | undefined = o;
  while (cur) {
    const id = cur.userData?.['furnitureId'];
    if (typeof id === 'number') return id;
    cur = cur.parent;
  }
  return null;
}

export function findItemInAncestors(o: SceneNode | null | undefined): Item | null {
  let cur: SceneNode | null | undefined = o;
  while (cur) {
    const it = cur.userData?.['item'] as Item | undefined;
    if (it) return it;
    cur = cur.parent;
  }
  return null;
}
