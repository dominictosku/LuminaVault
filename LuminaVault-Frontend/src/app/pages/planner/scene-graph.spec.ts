import { describe, expect, it } from 'vitest';
import { SceneNode, findFurnitureIdInAncestors, findItemInAncestors } from './scene-graph';

function node(userData: Record<string, unknown>, parent: SceneNode | null = null): SceneNode {
  return { userData, parent };
}

describe('findFurnitureIdInAncestors', () => {
  it('returns the id when it sits on the node itself', () => {
    expect(findFurnitureIdInAncestors(node({ furnitureId: 7 }))).toBe(7);
  });

  it('walks up to the nearest ancestor that carries the id (glTF subtree → group)', () => {
    const root = node({ furnitureId: 12 });
    const mid = node({}, root);
    const leaf = node({}, mid);
    expect(findFurnitureIdInAncestors(leaf)).toBe(12);
  });

  it('skips non-numeric ids and keeps walking up', () => {
    const root = node({ furnitureId: 3 });
    const leaf = node({ furnitureId: 'not-a-number' }, root);
    expect(findFurnitureIdInAncestors(leaf)).toBe(3);
  });

  it('returns null when nothing in the chain is tagged', () => {
    expect(findFurnitureIdInAncestors(node({}, node({})))).toBeNull();
    expect(findFurnitureIdInAncestors(null)).toBeNull();
  });
});

describe('findItemInAncestors', () => {
  it('returns the item object from the nearest tagged ancestor', () => {
    const item = { id: 42 } as any;
    const root = node({ item });
    const leaf = node({}, root);
    expect(findItemInAncestors(leaf)).toBe(item);
  });

  it('returns null when no ancestor carries an item', () => {
    expect(findItemInAncestors(node({ furnitureId: 1 }))).toBeNull();
    expect(findItemInAncestors(undefined)).toBeNull();
  });
});
