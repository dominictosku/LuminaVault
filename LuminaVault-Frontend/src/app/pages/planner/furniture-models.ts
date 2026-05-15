import * as THREE from 'three';
// GLTFLoader is dynamic-imported on first use (see ensureGltfLoader below).
// It's only needed when a furniture kind has a custom .glb in /models/ or when
// an item has a per-instance model URL — most users have neither, so the
// procedural-geometry fallback path stays a smaller initial chunk for the
// planner route.
import type { GLTFLoader as GLTFLoaderType } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { FurnitureKind } from '../../core/models';

export interface OpenTransform {
  obj: THREE.Object3D;
  closed: { position: THREE.Vector3; rotation: THREE.Euler };
  open: { position?: THREE.Vector3; rotation?: THREE.Euler };
}

export interface BuiltFurniture {
  group: THREE.Group;
  openables: OpenTransform[];
}

const palette: Record<FurnitureKind, { primary: number; accent: number; soft: number }> = {
  Cabinet:  { primary: 0x4f46e5, accent: 0x6366f1, soft: 0xe0e7ff },
  Drawer:   { primary: 0x7c3aed, accent: 0x8b5cf6, soft: 0xede9fe },
  Shelf:    { primary: 0x2563eb, accent: 0x3b82f6, soft: 0xdbeafe },
  Wardrobe: { primary: 0xa855f7, accent: 0xc084fc, soft: 0xf3e8ff },
  Desk:     { primary: 0x0891b2, accent: 0x06b6d4, soft: 0xcffafe },
  Table:    { primary: 0x059669, accent: 0x10b981, soft: 0xd1fae5 },
  Sofa:     { primary: 0xdb2777, accent: 0xec4899, soft: 0xfecdd3 },
  Bed:      { primary: 0xea580c, accent: 0xf97316, soft: 0xffedd5 },
  Box:      { primary: 0xca8a04, accent: 0xeab308, soft: 0xfef3c7 },
  Other:    { primary: 0x64748b, accent: 0x94a3b8, soft: 0xe2e8f0 },
};

function mat(color: number, opts: Partial<THREE.MeshStandardMaterialParameters> = {}) {
  return new THREE.MeshStandardMaterial({
    color, roughness: 0.55, metalness: 0.15,
    emissive: new THREE.Color(color).multiplyScalar(0.04),
    ...opts,
  });
}

function box(w: number, h: number, d: number, m: THREE.Material) {
  const me = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), m);
  me.castShadow = true; me.receiveShadow = true;
  return me;
}

function cyl(rTop: number, rBot: number, h: number, m: THREE.Material) {
  const me = new THREE.Mesh(new THREE.CylinderGeometry(rTop, rBot, h, 16), m);
  me.castShadow = true; me.receiveShadow = true;
  return me;
}

function tagFurnitureId(group: THREE.Object3D, id: number) {
  group.traverse(o => { o.userData['furnitureId'] = id; o.userData['kind'] ??= 'furniture'; });
}

export function applyFurnitureId(group: THREE.Object3D, id: number) { tagFurnitureId(group, id); }

// ───── Builders ─────

function cabinet(w: number, h: number, d: number, kind: FurnitureKind = 'Cabinet'): BuiltFurniture {
  const c = palette[kind];
  const g = new THREE.Group();
  const body = box(w, h, d, mat(c.primary, { roughness: 0.45 }));
  body.position.y = h / 2;
  g.add(body);

  // Crown molding
  const crown = box(w + 0.04, 0.05, d + 0.04, mat(c.accent));
  crown.position.y = h - 0.025;
  g.add(crown);

  // Plinth
  const plinth = box(w + 0.02, 0.06, d + 0.02, mat(c.accent, { roughness: 0.7 }));
  plinth.position.y = 0.03;
  g.add(plinth);

  // Two doors with handles
  const doorMat = mat(c.accent, { roughness: 0.3, metalness: 0.4 });
  const handleMat = mat(0xfacc15, { roughness: 0.2, metalness: 0.85 });
  const doorW = (w - 0.04) / 2;
  const doorH = h - 0.16;
  const doorD = 0.03;
  const openables: OpenTransform[] = [];

  for (let i = 0; i < 2; i++) {
    const door = new THREE.Group();
    const sign = i === 0 ? -1 : 1;
    // Hinge pivot at outer edge so door rotates outward
    door.position.set(sign * (w / 2 - 0.02), h / 2, d / 2 + doorD / 2);
    const panel = box(doorW, doorH, doorD, doorMat);
    panel.position.x = -sign * (doorW / 2);
    door.add(panel);
    const handle = cyl(0.012, 0.012, 0.08, handleMat);
    handle.rotation.x = Math.PI / 2;
    handle.position.set(-sign * (doorW * 0.85), 0, doorD * 0.6);
    door.add(handle);
    g.add(door);
    openables.push({
      obj: door,
      closed: { position: door.position.clone(), rotation: door.rotation.clone() },
      open: { rotation: new THREE.Euler(0, sign * Math.PI * 0.55, 0) },
    });
  }

  return { group: g, openables };
}

function drawerChest(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Drawer;
  const g = new THREE.Group();
  const body = box(w, h, d, mat(c.primary));
  body.position.y = h / 2;
  g.add(body);

  const drawers = 3;
  const drawerH = (h - 0.08) / drawers;
  const drawerMat = mat(c.accent, { roughness: 0.3, metalness: 0.35 });
  const handleMat = mat(0xfacc15, { roughness: 0.2, metalness: 0.85 });
  const openables: OpenTransform[] = [];

  for (let i = 0; i < drawers; i++) {
    const dr = new THREE.Group();
    const y = 0.04 + drawerH * (i + 0.5);
    dr.position.set(0, y, d / 2 + 0.025);
    const front = box(w - 0.06, drawerH - 0.02, 0.04, drawerMat);
    dr.add(front);
    const handle = box(w * 0.25, 0.025, 0.04, handleMat);
    handle.position.set(0, 0, 0.04);
    dr.add(handle);
    g.add(dr);
    openables.push({
      obj: dr,
      closed: { position: dr.position.clone(), rotation: dr.rotation.clone() },
      open: { position: dr.position.clone().add(new THREE.Vector3(0, 0, d * 0.55)) },
    });
  }

  // Plinth
  const plinth = box(w + 0.02, 0.06, d + 0.02, mat(c.accent));
  plinth.position.y = 0.03;
  g.add(plinth);

  return { group: g, openables };
}

function shelf(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Shelf;
  const g = new THREE.Group();
  const woodMat = mat(c.primary, { roughness: 0.6 });
  const accentMat = mat(c.accent);

  // Side posts
  const post = 0.04;
  const left = box(post, h, d, woodMat);
  left.position.set(-w / 2 + post / 2, h / 2, 0);
  g.add(left);
  const right = left.clone();
  right.position.x = w / 2 - post / 2;
  g.add(right);
  const back = box(w, h, 0.02, woodMat);
  back.position.set(0, h / 2, -d / 2 + 0.01);
  g.add(back);

  // Shelves (4 levels)
  const levels = 4;
  for (let i = 0; i < levels; i++) {
    const shelfBoard = box(w - post * 2 + 0.01, 0.03, d - 0.01, accentMat);
    shelfBoard.position.set(0, (h / levels) * (i + 0.5) - 0.015, 0);
    g.add(shelfBoard);
  }

  // Slide-out tray reveal: a faint glow plate that slides forward
  const trayMat = new THREE.MeshStandardMaterial({
    color: c.soft, transparent: true, opacity: 0.0,
    emissive: new THREE.Color(c.soft).multiplyScalar(0.5), emissiveIntensity: 0.3,
  });
  const tray = box(w - post * 2 - 0.04, 0.012, d - 0.05, trayMat);
  tray.position.set(0, h / 2, 0);
  g.add(tray);

  return {
    group: g,
    openables: [{
      obj: tray,
      closed: { position: tray.position.clone(), rotation: tray.rotation.clone() },
      open: { position: tray.position.clone().add(new THREE.Vector3(0, 0, d * 0.4)) },
    }],
  };
}

function wardrobe(w: number, h: number, d: number): BuiltFurniture {
  // Same pattern as cabinet but tall
  return cabinet(w, h, d, 'Wardrobe');
}

function desk(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Desk;
  const g = new THREE.Group();
  const woodMat = mat(c.primary, { roughness: 0.5 });
  const accentMat = mat(c.accent);

  // Top
  const topThick = 0.04;
  const top = box(w, topThick, d, woodMat);
  top.position.y = h - topThick / 2;
  g.add(top);

  // Legs (4)
  const legW = 0.06;
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const leg = box(legW, h - topThick, legW, accentMat);
    leg.position.set(sx * (w / 2 - legW), (h - topThick) / 2, sz * (d / 2 - legW));
    g.add(leg);
  }

  // Drawer under top, right side
  const drW = w * 0.35, drH = 0.18, drD = d * 0.7;
  const drawerMat = mat(c.accent, { roughness: 0.3 });
  const drawer = new THREE.Group();
  drawer.position.set(w / 2 - drW / 2 - 0.05, h - topThick - drH / 2 - 0.02, d / 2 + 0.02);
  const front = box(drW, drH, 0.04, drawerMat);
  drawer.add(front);
  const handle = box(drW * 0.4, 0.02, 0.03, mat(0xfacc15, { metalness: 0.8 }));
  handle.position.z = 0.035;
  drawer.add(handle);
  // Drawer body (sides) for realism
  const drawerBox = box(drW - 0.04, drH - 0.04, drD, mat(c.accent, { roughness: 0.7 }));
  drawerBox.position.z = -drD / 2 - 0.02;
  drawer.add(drawerBox);
  g.add(drawer);

  return {
    group: g,
    openables: [{
      obj: drawer,
      closed: { position: drawer.position.clone(), rotation: drawer.rotation.clone() },
      open: { position: drawer.position.clone().add(new THREE.Vector3(0, 0, d * 0.5)) },
    }],
  };
}

function table(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Table;
  const g = new THREE.Group();
  const woodMat = mat(c.primary, { roughness: 0.5 });
  const accentMat = mat(c.accent);

  const topThick = 0.05;
  const top = box(w, topThick, d, woodMat);
  top.position.y = h - topThick / 2;
  g.add(top);

  // Legs
  const legW = 0.07;
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const leg = box(legW, h - topThick, legW, accentMat);
    leg.position.set(sx * (w / 2 - legW), (h - topThick) / 2, sz * (d / 2 - legW));
    g.add(leg);
  }

  // Tabletop lifts up to "reveal"
  return {
    group: g,
    openables: [{
      obj: top,
      closed: { position: top.position.clone(), rotation: top.rotation.clone() },
      open: { position: top.position.clone().add(new THREE.Vector3(0, 0.4, 0)) },
    }],
  };
}

function sofa(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Sofa;
  const g = new THREE.Group();
  const fabric = mat(c.primary, { roughness: 0.85, metalness: 0.0 });
  const cushion = mat(c.soft, { roughness: 0.9 });

  const baseH = h * 0.4;
  const base = box(w, baseH, d, fabric);
  base.position.y = baseH / 2;
  g.add(base);

  // Backrest
  const backD = d * 0.22;
  const backH = h * 0.55;
  const back = box(w, backH, backD, fabric);
  back.position.set(0, baseH + backH / 2, -d / 2 + backD / 2);
  g.add(back);

  // Armrests
  const armW = w * 0.08;
  const armH = h * 0.6;
  for (const sx of [-1, 1]) {
    const arm = box(armW, armH, d, fabric);
    arm.position.set(sx * (w / 2 - armW / 2), armH / 2, 0);
    g.add(arm);
  }

  // Seat cushions (animate by lifting one)
  const cw = (w - armW * 2.5) / 2;
  const ch = h * 0.18;
  const openables: OpenTransform[] = [];
  for (let i = 0; i < 2; i++) {
    const cu = box(cw, ch, d - backD - 0.04, cushion);
    const x = (i - 0.5) * (cw + 0.04);
    cu.position.set(x, baseH + ch / 2, (backD - 0.04) / 2);
    g.add(cu);
    if (i === 0) {
      openables.push({
        obj: cu,
        closed: { position: cu.position.clone(), rotation: cu.rotation.clone() },
        open: {
          position: cu.position.clone().add(new THREE.Vector3(0, 0.35, 0.05)),
          rotation: new THREE.Euler(-0.4, 0, 0),
        },
      });
    }
  }

  // Legs
  const legR = 0.025;
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const leg = cyl(legR, legR, 0.08, mat(0x111827, { metalness: 0.7 }));
    leg.position.set(sx * (w / 2 - 0.08), 0.04, sz * (d / 2 - 0.08));
    g.add(leg);
  }

  return { group: g, openables };
}

function bed(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Bed;
  const g = new THREE.Group();
  const frameMat = mat(c.primary, { roughness: 0.5 });
  const mattressMat = mat(c.soft, { roughness: 0.95 });
  const pillowMat = mat(0xfafafa, { roughness: 0.9 });

  // Frame
  const frameH = h * 0.3;
  const frame = box(w, frameH, d, frameMat);
  frame.position.y = frameH / 2;
  g.add(frame);

  // Headboard
  const hbH = h * 0.85;
  const hb = box(w, hbH, 0.06, frameMat);
  hb.position.set(0, hbH / 2, -d / 2 - 0.03);
  g.add(hb);

  // Mattress (animate up)
  const mattH = h * 0.25;
  const mattress = box(w - 0.04, mattH, d - 0.04, mattressMat);
  mattress.position.y = frameH + mattH / 2;
  g.add(mattress);

  // Pillows
  for (let i = 0; i < 2; i++) {
    const pw = (w - 0.2) / 2;
    const ph = h * 0.08;
    const pd = d * 0.25;
    const p = box(pw, ph, pd, pillowMat);
    p.position.set((i - 0.5) * (pw + 0.05), frameH + mattH + ph / 2, -d / 2 + pd / 2 + 0.05);
    p.rotation.y = (i - 0.5) * 0.1;
    g.add(p);
  }

  // Legs
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const leg = box(0.05, frameH * 0.6, 0.05, mat(0x1f2937, { roughness: 0.4 }));
    leg.position.set(sx * (w / 2 - 0.05), frameH * 0.3, sz * (d / 2 - 0.05));
    g.add(leg);
  }

  return {
    group: g,
    openables: [{
      obj: mattress,
      closed: { position: mattress.position.clone(), rotation: mattress.rotation.clone() },
      open: { position: mattress.position.clone().add(new THREE.Vector3(0, 0.35, 0)),
              rotation: new THREE.Euler(-0.15, 0, 0) },
    }],
  };
}

function boxFurniture(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Box;
  const g = new THREE.Group();
  const bodyMat = mat(c.primary, { roughness: 0.6 });
  const lidMat = mat(c.accent, { roughness: 0.4 });

  const bodyH = h * 0.85;
  const body = box(w, bodyH, d, bodyMat);
  body.position.y = bodyH / 2;
  g.add(body);

  // Lid pivots at the back edge
  const lidH = h * 0.15;
  const lid = new THREE.Group();
  lid.position.set(0, bodyH, -d / 2);
  const lidMesh = box(w, lidH, d, lidMat);
  lidMesh.position.set(0, lidH / 2, d / 2);
  lid.add(lidMesh);
  g.add(lid);

  // Edge accent
  const trim = box(w + 0.02, 0.02, d + 0.02, mat(c.accent));
  trim.position.y = bodyH;
  g.add(trim);

  return {
    group: g,
    openables: [{
      obj: lid,
      closed: { position: lid.position.clone(), rotation: lid.rotation.clone() },
      open: { rotation: new THREE.Euler(-Math.PI * 0.55, 0, 0) },
    }],
  };
}

function generic(w: number, h: number, d: number): BuiltFurniture {
  const c = palette.Other;
  const g = new THREE.Group();
  const body = box(w, h, d, mat(c.primary));
  body.position.y = h / 2;
  g.add(body);
  // Top trim that lifts as a "reveal"
  const top = box(w * 0.95, 0.04, d * 0.95, mat(c.accent));
  top.position.y = h + 0.02;
  g.add(top);
  return {
    group: g,
    openables: [{
      obj: top,
      closed: { position: top.position.clone(), rotation: top.rotation.clone() },
      open: { position: top.position.clone().add(new THREE.Vector3(0, 0.25, 0)) },
    }],
  };
}

export function buildFurnitureMesh(kind: FurnitureKind, w: number, h: number, d: number): BuiltFurniture {
  switch (kind) {
    case 'Cabinet':  return cabinet(w, h, d);
    case 'Drawer':   return drawerChest(w, h, d);
    case 'Shelf':    return shelf(w, h, d);
    case 'Wardrobe': return wardrobe(w, h, d);
    case 'Desk':     return desk(w, h, d);
    case 'Table':    return table(w, h, d);
    case 'Sofa':     return sofa(w, h, d);
    case 'Bed':      return bed(w, h, d);
    case 'Box':      return boxFurniture(w, h, d);
    default:         return generic(w, h, d);
  }
}

// ───── glTF loader with cache + 404 fallback ─────

// Lazy-instantiated to defer the GLTFLoader import. Memoised so we pay
// the dynamic import + parse cost only once per page session.
let loaderPromise: Promise<GLTFLoaderType> | null = null;
function ensureGltfLoader(): Promise<GLTFLoaderType> {
  loaderPromise ??= import('three/examples/jsm/loaders/GLTFLoader.js')
    .then(mod => new mod.GLTFLoader());
  return loaderPromise;
}

const cache = new Map<FurnitureKind, Promise<THREE.Object3D | null>>();

export function loadModelForKind(kind: FurnitureKind): Promise<THREE.Object3D | null> {
  if (cache.has(kind)) return cache.get(kind)!;
  const url = `/models/${kind}.glb`;
  const p = fetch(url, { method: 'HEAD' })
    .then(async r => {
      if (!r.ok) return null;
      const loader = await ensureGltfLoader();
      return new Promise<THREE.Object3D | null>((resolve) => {
        loader.load(url, gltf => resolve(gltf.scene), undefined, () => resolve(null));
      });
    })
    .catch(() => null);
  cache.set(kind, p);
  return p;
}

/** Load a glTF/glb from an arbitrary URL (no caching — caller decides). */
export async function loadModelFromUrl(url: string): Promise<THREE.Object3D | null> {
  const loader = await ensureGltfLoader();
  return new Promise(resolve => {
    loader.load(url, gltf => resolve(gltf.scene), undefined, () => resolve(null));
  });
}

/** Scale a model so its largest dimension equals `maxDim`, then center it at the origin. */
export function fitCentered(root: THREE.Object3D, maxDim: number) {
  const bbox = new THREE.Box3().setFromObject(root);
  const size = new THREE.Vector3(); bbox.getSize(size);
  const m = Math.max(size.x, size.y, size.z);
  if (m === 0) return;
  root.scale.setScalar(maxDim / m);
  const bbox2 = new THREE.Box3().setFromObject(root);
  const center = new THREE.Vector3(); bbox2.getCenter(center);
  root.position.sub(center);
  root.traverse(o => {
    if ((o as THREE.Mesh).isMesh) {
      o.castShadow = true;
      o.receiveShadow = true;
    }
  });
}

/** Scale and center a loaded glTF root so it occupies a w×h×d box with feet on y=0, centered on (0,0). */
export function fitInto(root: THREE.Object3D, w: number, h: number, d: number) {
  const bbox = new THREE.Box3().setFromObject(root);
  const size = new THREE.Vector3(); bbox.getSize(size);
  if (size.x === 0 || size.y === 0 || size.z === 0) return;
  const sx = w / size.x, sy = h / size.y, sz = d / size.z;
  const s = Math.min(sx, sy, sz);
  root.scale.setScalar(s);

  // Recompute and center on x/z, place feet on y=0
  const bbox2 = new THREE.Box3().setFromObject(root);
  const center = new THREE.Vector3(); bbox2.getCenter(center);
  root.position.x -= center.x;
  root.position.z -= center.z;
  root.position.y -= bbox2.min.y;

  root.traverse(o => {
    if ((o as THREE.Mesh).isMesh) {
      o.castShadow = true;
      o.receiveShadow = true;
    }
  });
}
