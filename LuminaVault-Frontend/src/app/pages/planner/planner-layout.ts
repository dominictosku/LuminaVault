/// Pure geometry/layout math for the 3D planner. No Three.js or DOM dependency on
/// purpose: this is the load-bearing math (marker rings, wall specs, camera framing,
/// star dome) lifted out of the scene-graph object soup so it can be unit-tested and
/// reasoned about in isolation. PlannerScene converts the plain vectors below into
/// THREE.Vector3 at the call sites.

export interface Vec3 {
  x: number;
  y: number;
  z: number;
}

export interface WallSpec {
  w: number;
  h: number;
  d: number;
  x: number;
  z: number;
}

export interface CameraView {
  camPos: Vec3;
  target: Vec3;
}

const TWO_PI = Math.PI * 2;

/// Ring placement for item markers floating above a furniture piece (furniture-local
/// coords). idx/count spread markers evenly around a circle; the (idx % 3) stagger in
/// Y keeps overlapping orbs from z-fighting.
export function furnitureMarkerPosition(
  idx: number, count: number, width: number, depth: number, height: number,
): Vec3 {
  const angle = (idx / Math.max(count, 1)) * TWO_PI;
  const radius = Math.min(width, depth) * 0.35;
  return {
    x: Math.cos(angle) * radius,
    y: height + 0.3 + (idx % 3) * 0.05,
    z: Math.sin(angle) * radius,
  };
}

/// Ring placement for "loose" items pinned to a room rather than a furniture piece
/// (room-local coords, origin at the room's near corner).
export function looseMarkerPosition(idx: number, count: number, width: number, depth: number): Vec3 {
  const radius = Math.min(width, depth) * 0.18;
  const angle = (idx / Math.max(count, 1)) * TWO_PI;
  return {
    x: width / 2 + Math.cos(angle) * radius,
    y: 1.35 + (idx % 3) * 0.08,
    z: depth / 2 + Math.sin(angle) * radius,
  };
}

/// The four translucent walls of a room as box specs (room-local coords).
export function roomWallSpecs(width: number, height: number, depth: number, thickness = 0.05): WallSpec[] {
  const t = thickness;
  return [
    { w: width, h: height, d: t, x: width / 2, z: 0 },
    { w: width, h: height, d: t, x: width / 2, z: depth },
    { w: t, h: height, d: depth, x: 0, z: depth / 2 },
    { w: t, h: height, d: depth, x: width, z: depth / 2 },
  ];
}

/// Camera placement that frames a whole room from a back-corner three-quarter angle.
export function roomCameraView(
  room: { x: number; z: number; width: number; depth: number; height: number },
): CameraView {
  const cx = room.x + room.width / 2;
  const cz = room.z + room.depth / 2;
  const dist = Math.max(room.width, room.depth) * 1.4;
  return {
    camPos: { x: cx + dist * 0.6, y: room.height + dist * 0.8, z: cz + dist * 0.9 },
    target: { x: cx, y: room.height / 2, z: cz },
  };
}

/// Camera placement standing in front of a furniture piece, looking at its mid-height.
/// `forward` is the +Z axis rotated by the piece's Y rotation: (sin θ, 0, cos θ).
export function furnitureCameraView(worldPos: Vec3, rotationY: number, height: number): CameraView {
  const fwdX = Math.sin(rotationY);
  const fwdZ = Math.cos(rotationY);
  return {
    camPos: {
      x: worldPos.x + fwdX * 2.4,
      y: worldPos.y + height + 0.6,
      z: worldPos.z + fwdZ * 2.4,
    },
    target: { x: worldPos.x, y: worldPos.y + height / 2, z: worldPos.z },
  };
}

/// Generate a dome of star positions as a flat [x, y, z, ...] buffer. `rng` is
/// injectable so tests are deterministic; the app passes Math.random.
export function starFieldPositions(count: number, rng: () => number = Math.random): Float32Array {
  const positions = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    const r = 60 + rng() * 30;
    const theta = rng() * TWO_PI;
    const phi = Math.acos(2 * rng() - 1);
    positions[i * 3 + 0] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = Math.abs(r * Math.cos(phi));
    positions[i * 3 + 2] = r * Math.sin(phi) * Math.sin(theta);
  }
  return positions;
}
