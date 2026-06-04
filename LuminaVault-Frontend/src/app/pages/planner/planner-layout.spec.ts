import { describe, expect, it } from 'vitest';
import {
  furnitureCameraView,
  furnitureMarkerPosition,
  looseMarkerPosition,
  roomCameraView,
  roomWallSpecs,
  starFieldPositions,
} from './planner-layout';

describe('furnitureMarkerPosition', () => {
  it('places the first marker on the +X spoke at the ring radius', () => {
    const p = furnitureMarkerPosition(0, 4, 2, 1, 1);
    // radius = min(2,1) * 0.35 = 0.35; angle 0 → cos 1, sin 0
    expect(p.x).toBeCloseTo(0.35);
    expect(p.z).toBeCloseTo(0);
    expect(p.y).toBeCloseTo(1.3); // height 1 + 0.3 + (0 % 3)*0.05
  });

  it('spreads markers evenly around the circle and staggers Y by idx % 3', () => {
    const p = furnitureMarkerPosition(1, 4, 2, 1, 1); // angle π/2 → cos 0, sin 1
    expect(p.x).toBeCloseTo(0);
    expect(p.z).toBeCloseTo(0.35);
    expect(p.y).toBeCloseTo(1.35); // + (1 % 3)*0.05
  });

  it('treats a count of 0 as 1 so a lone marker does not divide by zero', () => {
    expect(() => furnitureMarkerPosition(0, 0, 2, 2, 1)).not.toThrow();
    const p = furnitureMarkerPosition(0, 0, 2, 2, 1);
    expect(Number.isFinite(p.x)).toBe(true);
  });
});

describe('looseMarkerPosition', () => {
  it('places loose items on a ring centred on the room (room-local coords)', () => {
    const p = looseMarkerPosition(0, 2, 4, 4);
    // radius = 4 * 0.18 = 0.72; centre = (2, 2); angle 0
    expect(p.x).toBeCloseTo(2.72);
    expect(p.z).toBeCloseTo(2);
    expect(p.y).toBeCloseTo(1.35);
  });

  it('staggers Y by idx % 3', () => {
    expect(looseMarkerPosition(1, 3, 4, 4).y).toBeCloseTo(1.43);
    expect(looseMarkerPosition(3, 4, 4, 4).y).toBeCloseTo(1.35); // 3 % 3 === 0
  });
});

describe('roomWallSpecs', () => {
  it('returns four walls: two full-width front/back, two thin left/right', () => {
    const specs = roomWallSpecs(4, 3, 2);
    expect(specs).toHaveLength(4);
    expect(specs[0]).toEqual({ w: 4, h: 3, d: 0.05, x: 2, z: 0 });
    expect(specs[1]).toEqual({ w: 4, h: 3, d: 0.05, x: 2, z: 2 });
    expect(specs[2]).toEqual({ w: 0.05, h: 3, d: 2, x: 0, z: 1 });
    expect(specs[3]).toEqual({ w: 0.05, h: 3, d: 2, x: 4, z: 1 });
  });

  it('honours a custom wall thickness', () => {
    const specs = roomWallSpecs(4, 3, 2, 0.2);
    expect(specs[0].d).toBe(0.2);
    expect(specs[2].w).toBe(0.2);
  });
});

describe('roomCameraView', () => {
  it('frames the room centre from a back-corner three-quarter angle', () => {
    const view = roomCameraView({ x: 0, z: 0, width: 4, depth: 2, height: 3 });
    // centre (2,1); dist = max(4,2) * 1.4 = 5.6
    expect(view.target).toEqual({ x: 2, y: 1.5, z: 1 });
    expect(view.camPos.x).toBeCloseTo(5.36); // 2 + 5.6*0.6
    expect(view.camPos.y).toBeCloseTo(7.48); // 3 + 5.6*0.8
    expect(view.camPos.z).toBeCloseTo(6.04); // 1 + 5.6*0.9
  });
});

describe('furnitureCameraView', () => {
  it('stands in front along +Z when unrotated, eyeing the mid-height', () => {
    const view = furnitureCameraView({ x: 1, y: 0, z: 2 }, 0, 1);
    expect(view.camPos.x).toBeCloseTo(1);
    expect(view.camPos.y).toBeCloseTo(1.6); // y + height + 0.6
    expect(view.camPos.z).toBeCloseTo(4.4); // z + 2.4
    expect(view.target).toEqual({ x: 1, y: 0.5, z: 2 });
  });

  it('rotates the standing offset by the furniture Y rotation', () => {
    const view = furnitureCameraView({ x: 1, y: 0, z: 2 }, Math.PI / 2, 1);
    // forward = (sin, cos) = (1, 0) → offset on +X
    expect(view.camPos.x).toBeCloseTo(3.4); // 1 + 2.4
    expect(view.camPos.z).toBeCloseTo(2); // 2 + ~0
  });
});

describe('starFieldPositions', () => {
  it('emits a flat x,y,z buffer of the requested length', () => {
    const pos = starFieldPositions(10, () => 0.5);
    expect(pos).toBeInstanceOf(Float32Array);
    expect(pos.length).toBe(30);
  });

  it('draws exactly three random values per star and is deterministic for a fixed rng', () => {
    let calls = 0;
    const rng = () => { calls++; return 0.5; };
    const pos = starFieldPositions(2, rng);
    expect(calls).toBe(6);
    // r = 75, theta = π, phi = π/2 → x = -75, y = 0
    expect(pos[0]).toBeCloseTo(-75);
    expect(pos[1]).toBeCloseTo(0);
  });

  it('keeps the dome above the floor (y is always non-negative)', () => {
    const pos = starFieldPositions(200);
    for (let i = 0; i < 200; i++) expect(pos[i * 3 + 1]).toBeGreaterThanOrEqual(0);
  });
});
