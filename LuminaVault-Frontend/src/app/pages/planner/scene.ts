import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import gsap from 'gsap';
import { Furniture, Item, Room } from '../../core/models';
import { API_BASE } from '../../core/api-base';
import { applyFurnitureId, BuiltFurniture, buildFurnitureMesh, fitCentered, fitInto, loadModelForKind, loadModelFromUrl, OpenTransform } from './furniture-models';
import { furnitureCameraView, furnitureMarkerPosition, looseMarkerPosition, roomCameraView, roomWallSpecs } from './planner-layout';
import { findFurnitureIdInAncestors, findItemInAncestors } from './scene-graph';
import { createEnvironment } from './scene-environment';

export type PlannerMode = 'overview' | 'room' | 'furniture';

export interface PlannerState {
  mode: PlannerMode;
  room?: Room;
  furniture?: Furniture;
}

export interface SceneCallbacks {
  onSelectRoom: (room: Room) => void;
  onSelectFurniture: (f: Furniture) => void;
  onSelectItem: (i: Item) => void;
  onHoverItem: (i: Item | null) => void;
  onMoveFurniture: (f: Furniture, x: number, z: number) => void;
  authToken: () => string | null;
}

interface RoomMesh {
  data: Room;
  group: THREE.Group;
  floor: THREE.Mesh;
  walls: THREE.Mesh[];
  glowEdges: THREE.LineSegments;
  label: THREE.Sprite;
  looseItemMarkers: THREE.Mesh[];
}

interface FurnitureMesh {
  data: Furniture;
  group: THREE.Group;
  modelHolder: THREE.Group;
  openables: OpenTransform[];
  itemMarkers: THREE.Mesh[];
}

export class PlannerScene {
  private renderer!: THREE.WebGLRenderer;
  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private controls!: OrbitControls;
  private raycaster = new THREE.Raycaster();
  private pointer = new THREE.Vector2();
  private host: HTMLElement;
  private resizeObs!: ResizeObserver;
  private rafId = 0;
  private clock = new THREE.Clock();

  private rooms: RoomMesh[] = [];
  private furniture: FurnitureMesh[] = [];
  private itemsByFurniture = new Map<number, Item[]>();
  private looseItemsByRoom = new Map<number, Item[]>();

  private cb: SceneCallbacks;
  private state: PlannerState = { mode: 'overview' };
  private hoveredItem: Item | null = null;

  // Drag state for moving furniture
  private dragging: { mesh: FurnitureMesh; offset: THREE.Vector3; plane: THREE.Plane } | null = null;
  private starField!: THREE.Points;

  constructor(host: HTMLElement, cb: SceneCallbacks) {
    this.host = host;
    this.cb = cb;
    this.init();
  }

  private init() {
    const w = this.host.clientWidth, h = this.host.clientHeight;

    this.scene = new THREE.Scene();
    this.scene.fog = new THREE.FogExp2(0x0b0a13, 0.035);

    this.camera = new THREE.PerspectiveCamera(55, w / h, 0.1, 200);
    this.camera.position.set(12, 14, 16);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(w, h);
    this.renderer.setClearColor(0x0b0a13, 1);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.host.appendChild(this.renderer.domElement);

    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.minDistance = 1.5;
    this.controls.maxDistance = 60;
    this.controls.maxPolarAngle = Math.PI * 0.49;

    // Lighting rig, ground, grid, and star dome.
    const env = createEnvironment(this.scene);
    this.starField = env.starField;

    // Events
    this.renderer.domElement.addEventListener('pointermove', this.onPointerMove);
    this.renderer.domElement.addEventListener('pointerdown', this.onPointerDown);
    this.renderer.domElement.addEventListener('pointerup', this.onPointerUp);
    this.renderer.domElement.addEventListener('click', this.onClick);

    this.resizeObs = new ResizeObserver(() => this.onResize());
    this.resizeObs.observe(this.host);

    this.animate();
  }

  setData(rooms: Room[], furniture: Furniture[], items: Item[]) {
    // Clear previous
    for (const r of this.rooms) this.scene.remove(r.group);
    for (const f of this.furniture) this.scene.remove(f.group);
    this.rooms = [];
    this.furniture = [];
    this.itemsByFurniture.clear();
    this.looseItemsByRoom.clear();

    // Group items
    for (const it of items) {
      if (it.furnitureId) {
        if (!this.itemsByFurniture.has(it.furnitureId)) this.itemsByFurniture.set(it.furnitureId, []);
        this.itemsByFurniture.get(it.furnitureId)!.push(it);
      } else if (it.roomId) {
        if (!this.looseItemsByRoom.has(it.roomId)) this.looseItemsByRoom.set(it.roomId, []);
        this.looseItemsByRoom.get(it.roomId)!.push(it);
      }
    }

    for (const r of rooms) this.rooms.push(this.buildRoom(r));
    for (const f of furniture) this.furniture.push(this.buildFurniture(f));

    this.applyMode();
  }

  private buildRoom(r: Room): RoomMesh {
    const g = new THREE.Group();
    g.position.set(r.x, 0, r.z);
    g.userData['kind'] = 'room';
    g.userData['roomId'] = r.id;

    const colorObj = new THREE.Color(r.color || '#7c3aed');

    // Floor
    const floorMat = new THREE.MeshStandardMaterial({
      color: colorObj.clone().multiplyScalar(0.35),
      roughness: 0.7, metalness: 0.1,
      emissive: colorObj.clone().multiplyScalar(0.1),
    });
    const floor = new THREE.Mesh(new THREE.BoxGeometry(r.width, 0.05, r.depth), floorMat);
    floor.position.set(r.width/2, 0.025, r.depth/2);
    floor.receiveShadow = true;
    floor.userData['kind'] = 'room';
    floor.userData['roomId'] = r.id;
    g.add(floor);

    // Walls (translucent)
    const wallMat = new THREE.MeshPhysicalMaterial({
      color: 0xffffff, metalness: 0.0, roughness: 0.2,
      transmission: 0.92, transparent: true, opacity: 0.18,
      thickness: 0.4, side: THREE.DoubleSide,
    });
    const walls: THREE.Mesh[] = [];
    const wallSpecs = roomWallSpecs(r.width, r.height, r.depth);
    for (const s of wallSpecs) {
      const m = new THREE.Mesh(new THREE.BoxGeometry(s.w, s.h, s.d), wallMat);
      m.position.set(s.x, s.h/2, s.z);
      m.userData['kind'] = 'wall';
      m.userData['roomId'] = r.id;
      walls.push(m);
      g.add(m);
    }

    // Glowing edge frame
    const edgeGeo = new THREE.EdgesGeometry(new THREE.BoxGeometry(r.width, r.height, r.depth));
    const edgeMat = new THREE.LineBasicMaterial({ color: colorObj, transparent: true, opacity: 0.85 });
    const edges = new THREE.LineSegments(edgeGeo, edgeMat);
    edges.position.set(r.width/2, r.height/2, r.depth/2);
    g.add(edges);

    // Label
    const label = makeLabelSprite(r.name);
    label.position.set(r.width/2, r.height + 0.6, r.depth/2);
    g.add(label);

    // Loose item markers — items linked directly to the room (no furniture)
    const looseItems = this.looseItemsByRoom.get(r.id) ?? [];
    const looseItemMarkers: THREE.Mesh[] = [];
    looseItems.forEach((it, idx) => {
      const p = looseMarkerPosition(idx, looseItems.length, r.width, r.depth);
      const orb = this.createItemMarker(it, new THREE.Vector3(p.x, p.y, p.z), {
        loose: true, modelMaxDim: 0.5,
      });
      g.add(orb);
      looseItemMarkers.push(orb);
    });

    this.scene.add(g);
    return { data: r, group: g, floor, walls, glowEdges: edges, label, looseItemMarkers };
  }

  /**
   * Build a marker for an item. Always a placeholder orb so it bobs and animates the same way.
   * If the item has an uploaded model, the model is loaded async and added as a child of the orb;
   * the orb itself is then made visually invisible and excluded from raycasting (the model becomes
   * the click target via parent-walk).
   */
  private createItemMarker(item: Item, basePos: THREE.Vector3,
                            opts: { loose: boolean; modelMaxDim: number }): THREE.Mesh {
    const orb = new THREE.Mesh(
      new THREE.IcosahedronGeometry(opts.loose ? 0.09 : 0.07, 1),
      new THREE.MeshStandardMaterial({
        color: 0xffffff,
        emissive: opts.loose ? 0xff6fb3 : 0xa78bfa,
        emissiveIntensity: opts.loose ? 1.4 : 1.2,
        roughness: 0.2, metalness: 0.8,
        transparent: true, opacity: 0,
      })
    );
    orb.position.copy(basePos);
    orb.userData['kind'] = opts.loose ? 'loose-item' : 'item';
    orb.userData['itemId'] = item.id;
    orb.userData['item'] = item;
    orb.userData['basePos'] = basePos.clone();
    orb.userData['bobOffset'] = Math.random() * Math.PI * 2;
    orb.visible = false;

    if (item.modelUrl) {
      const cacheBust = encodeURIComponent(item.updatedAt ?? '');
      const url = `${API_BASE}${item.modelUrl}?t=${cacheBust}`;
      const token = this.cb.authToken();
      const headers = token ? { Authorization: `Bearer ${token}` } : undefined;
      loadModelFromUrl(url, headers).then(loaded => {
        if (!loaded) return;
        const root = loaded;
        fitCentered(root, opts.modelMaxDim);
        // Tag descendants with item id so click/hover walks back to the marker
        root.traverse(o => { o.userData['itemId'] = item.id; o.userData['item'] = item; });
        orb.add(root);
        // Hide the placeholder orb visually + remove its raycast hit
        (orb.material as THREE.Material).visible = false;
        orb.raycast = () => {};
      }).catch(() => { /* keep orb */ });
    }

    return orb;
  }

  private buildFurniture(f: Furniture): FurnitureMesh {
    const room = this.rooms.find(rm => rm.data.id === f.roomId);
    const wx = (room?.data.x ?? 0) + f.x;
    const wz = (room?.data.z ?? 0) + f.z;

    const g = new THREE.Group();
    g.position.set(wx + f.width/2, f.y, wz + f.depth/2);
    g.rotation.y = f.rotationY;
    g.userData['kind'] = 'furniture';
    g.userData['furnitureId'] = f.id;

    // Procedural model spans -w/2..+w/2, 0..h, -d/2..+d/2
    const modelHolder = new THREE.Group();
    const built: BuiltFurniture = buildFurnitureMesh(f.kind, f.width, f.height, f.depth);
    modelHolder.add(built.group);
    applyFurnitureId(modelHolder, f.id);
    g.add(modelHolder);

    // glTF override: if /models/{Kind}.glb exists, swap in. Procedural shows immediately as fallback.
    const procGroup = built.group;
    const openables = built.openables;
    loadModelForKind(f.kind).then(loaded => {
      if (!loaded) return;
      modelHolder.remove(procGroup);
      const root = loaded.clone(true);
      fitInto(root, f.width, f.height, f.depth);
      modelHolder.add(root);
      applyFurnitureId(modelHolder, f.id);
    }).catch(() => { /* keep procedural */ });

    // Item markers (orbs floating above the furniture)
    const items = this.itemsByFurniture.get(f.id) ?? [];
    const markers: THREE.Mesh[] = [];
    items.forEach((it, idx) => {
      const p = furnitureMarkerPosition(idx, items.length, f.width, f.depth, f.height);
      const basePos = new THREE.Vector3(p.x, p.y, p.z);
      const marker = this.createItemMarker(it, basePos, { loose: false, modelMaxDim: 0.28 });
      g.add(marker);
      markers.push(marker);
    });

    this.scene.add(g);
    return { data: f, group: g, modelHolder, openables, itemMarkers: markers };
  }

  setMode(state: PlannerState) {
    this.state = state;
    this.applyMode();
  }

  private applyMode() {
    const s = this.state;
    // Helpers shared across modes
    const closeAllFurniture = () => {
      for (const f of this.furniture) for (const o of f.openables) {
        gsap.to(o.obj.position, {
          x: o.closed.position.x, y: o.closed.position.y, z: o.closed.position.z,
          duration: 0.6, ease: 'power2.inOut',
        });
        gsap.to(o.obj.rotation, {
          x: o.closed.rotation.x, y: o.closed.rotation.y, z: o.closed.rotation.z,
          duration: 0.6, ease: 'power2.inOut',
        });
      }
    };
    const fadeMarker = (m: THREE.Mesh, target: number, duration: number, delay = 0) => {
      const mats: THREE.Material[] = [];
      m.traverse(o => {
        const mat = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
        if (!mat) return;
        const arr = Array.isArray(mat) ? mat : [mat];
        for (const a of arr) { a.transparent = true; mats.push(a); }
      });
      for (const mat of mats) gsap.to(mat as any, { opacity: target, duration, delay });
    };
    const hideAllFurnitureItems = () => {
      this.furniture.forEach(f => f.itemMarkers.forEach(m => {
        fadeMarker(m, 0, 0.3);
        gsap.delayedCall(0.3, () => { m.visible = false; });
      }));
    };
    const hideAllLooseItems = () => {
      this.rooms.forEach(r => r.looseItemMarkers.forEach(m => {
        fadeMarker(m, 0, 0.3);
        gsap.delayedCall(0.3, () => { m.visible = false; });
      }));
    };
    const revealMarkers = (markers: THREE.Mesh[]) => {
      markers.forEach((m, i) => {
        m.visible = true;
        const target = (m.userData['basePos'] as THREE.Vector3).clone();
        m.position.set(target.x, target.y - 0.5, target.z);
        fadeMarker(m, 1, 0.4, i * 0.04);
        gsap.to(m.position, { y: target.y, duration: 0.6, delay: i * 0.04, ease: 'back.out(1.6)' });
      });
    };

    if (s.mode === 'overview') {
      this.flyTo({ camPos: new THREE.Vector3(12, 14, 16), target: new THREE.Vector3(2, 0, 2) });
      for (const r of this.rooms) {
        for (const w of r.walls) gsap.to(w.material as any, { opacity: 0.18, duration: 0.6 });
        gsap.to(r.glowEdges.material as any, { opacity: 0.85, duration: 0.6 });
      }
      hideAllFurnitureItems();
      hideAllLooseItems();
      closeAllFurniture();
    }

    if (s.mode === 'room' && s.room) {
      const room = this.rooms.find(rm => rm.data.id === s.room!.id);
      if (!room) return;
      const view = roomCameraView(room.data);
      this.flyTo({
        camPos: new THREE.Vector3(view.camPos.x, view.camPos.y, view.camPos.z),
        target: new THREE.Vector3(view.target.x, view.target.y, view.target.z),
      });
      // Drop walls of the focused room
      for (const r of this.rooms) {
        const focused = r.data.id === s.room.id;
        for (const w of r.walls) gsap.to(w.material as any, { opacity: focused ? 0.05 : 0.02, duration: 0.5 });
        gsap.to(r.glowEdges.material as any, { opacity: focused ? 1 : 0.15, duration: 0.5 });
      }
      hideAllFurnitureItems();
      closeAllFurniture();

      // Reveal loose items in this room only, hide others
      this.rooms.forEach(r => {
        if (r.data.id === s.room!.id) {
          revealMarkers(r.looseItemMarkers);
        } else {
          r.looseItemMarkers.forEach(m => {
            gsap.to(m.material as any, { opacity: 0, duration: 0.3, onComplete: () => { m.visible = false; } });
          });
        }
      });
    }

    if (s.mode === 'furniture' && s.furniture) {
      const fmesh = this.furniture.find(fm => fm.data.id === s.furniture!.id);
      if (!fmesh) return;
      const wp = fmesh.group.position;
      const view = furnitureCameraView({ x: wp.x, y: wp.y, z: wp.z }, fmesh.group.rotation.y, fmesh.data.height);
      this.flyTo({
        camPos: new THREE.Vector3(view.camPos.x, view.camPos.y, view.camPos.z),
        target: new THREE.Vector3(view.target.x, view.target.y, view.target.z),
      });

      // Close other furniture, open the focused one
      for (const f of this.furniture) {
        const opening = f.data.id === fmesh.data.id;
        for (const o of f.openables) {
          const target = opening ? o.open : { position: o.closed.position, rotation: o.closed.rotation };
          if (target.position) {
            gsap.to(o.obj.position, { x: target.position.x, y: target.position.y, z: target.position.z,
              duration: 0.9, ease: opening ? 'power3.out' : 'power2.inOut' });
          } else if (!opening) {
            gsap.to(o.obj.position, { x: o.closed.position.x, y: o.closed.position.y, z: o.closed.position.z,
              duration: 0.6, ease: 'power2.inOut' });
          }
          if (target.rotation) {
            gsap.to(o.obj.rotation, { x: target.rotation.x, y: target.rotation.y, z: target.rotation.z,
              duration: 0.9, ease: opening ? 'power3.out' : 'power2.inOut' });
          } else if (!opening) {
            gsap.to(o.obj.rotation, { x: o.closed.rotation.x, y: o.closed.rotation.y, z: o.closed.rotation.z,
              duration: 0.6, ease: 'power2.inOut' });
          }
        }
      }

      // Reveal items in this furniture, hide others
      revealMarkers(fmesh.itemMarkers);
      this.furniture.filter(f => f.data.id !== fmesh.data.id)
        .forEach(f => f.itemMarkers.forEach(m => {
          fadeMarker(m, 0, 0.3);
          gsap.delayedCall(0.3, () => { m.visible = false; });
        }));
      hideAllLooseItems();
    }
  }

  private flyTo(opts: { camPos: THREE.Vector3; target: THREE.Vector3 }) {
    const tl = gsap.timeline();
    tl.to(this.camera.position, {
      x: opts.camPos.x, y: opts.camPos.y, z: opts.camPos.z,
      duration: 1.2, ease: 'power3.inOut',
      onUpdate: () => this.controls.update(),
    }, 0);
    tl.to(this.controls.target, {
      x: opts.target.x, y: opts.target.y, z: opts.target.z,
      duration: 1.2, ease: 'power3.inOut',
      onUpdate: () => this.controls.update(),
    }, 0);
  }

  private onPointerMove = (e: PointerEvent) => {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;

    if (this.dragging) {
      this.raycaster.setFromCamera(this.pointer, this.camera);
      const hit = new THREE.Vector3();
      if (this.raycaster.ray.intersectPlane(this.dragging.plane, hit)) {
        const room = this.rooms.find(r => r.data.id === this.dragging!.mesh.data.roomId);
        if (room) {
          const localX = hit.x - room.data.x - this.dragging.offset.x;
          const localZ = hit.z - room.data.z - this.dragging.offset.z;
          const f = this.dragging.mesh.data;
          const clampedX = Math.max(0, Math.min(room.data.width - f.width, localX));
          const clampedZ = Math.max(0, Math.min(room.data.depth - f.depth, localZ));
          this.dragging.mesh.group.position.x = room.data.x + clampedX + f.width/2;
          this.dragging.mesh.group.position.z = room.data.z + clampedZ + f.depth/2;
          f.x = clampedX;
          f.z = clampedZ;
        }
      }
      return;
    }

    // Hover items in furniture mode (orbs above the furniture) or in room mode (loose items)
    let hoverTargets: THREE.Mesh[] | null = null;
    if (this.state.mode === 'furniture' && this.state.furniture) {
      const fmesh = this.furniture.find(fm => fm.data.id === this.state.furniture!.id);
      if (fmesh) hoverTargets = fmesh.itemMarkers;
    } else if (this.state.mode === 'room' && this.state.room) {
      const room = this.rooms.find(rm => rm.data.id === this.state.room!.id);
      if (room) hoverTargets = room.looseItemMarkers;
    }
    if (hoverTargets) {
      this.raycaster.setFromCamera(this.pointer, this.camera);
      const hits = this.raycaster.intersectObjects(hoverTargets, true);
      const hovered = hits.length ? findItemInAncestors(hits[0].object) : null;
      if (hovered !== this.hoveredItem) {
        this.hoveredItem = hovered;
        this.cb.onHoverItem(hovered);
      }
    }
  };

  private onPointerDown = (e: PointerEvent) => {
    if (this.state.mode !== 'overview') return;
    if (e.button !== 0) return;
    if (!e.shiftKey) return; // Drag requires shift to avoid hijacking single-click selection
    this.raycaster.setFromCamera(this.pointer, this.camera);
    const groups = this.furniture.map(f => f.group);
    const hits = this.raycaster.intersectObjects(groups, true);
    if (!hits.length) return;
    const fid = findFurnitureIdInAncestors(hits[0].object);
    if (fid == null) return;
    const fmesh = this.furniture.find(fm => fm.data.id === fid);
    if (!fmesh) return;
    e.preventDefault();
    this.controls.enabled = false;
    const plane = new THREE.Plane(new THREE.Vector3(0,1,0), 0);
    const hit = new THREE.Vector3();
    this.raycaster.ray.intersectPlane(plane, hit);
    const room = this.rooms.find(r => r.data.id === fmesh.data.roomId);
    const localOffsetX = hit.x - (room ? room.data.x + fmesh.data.x : fmesh.group.position.x);
    const localOffsetZ = hit.z - (room ? room.data.z + fmesh.data.z : fmesh.group.position.z);
    this.dragging = { mesh: fmesh, offset: new THREE.Vector3(localOffsetX, 0, localOffsetZ), plane };
  };

  private onPointerUp = () => {
    if (this.dragging) {
      this.cb.onMoveFurniture(this.dragging.mesh.data, this.dragging.mesh.data.x, this.dragging.mesh.data.z);
    }
    this.dragging = null;
    this.controls.enabled = true;
  };

  private onClick = (e: MouseEvent) => {
    if (this.dragging) return;
    this.raycaster.setFromCamera(this.pointer, this.camera);

    // Item click in furniture mode (orbs or models)
    if (this.state.mode === 'furniture' && this.state.furniture) {
      const fmesh = this.furniture.find(fm => fm.data.id === this.state.furniture!.id);
      if (fmesh) {
        const itemHits = this.raycaster.intersectObjects(fmesh.itemMarkers, true);
        if (itemHits.length) {
          const it = findItemInAncestors(itemHits[0].object);
          if (it) {
            this.spawnItemBurst(itemHits[0].object.getWorldPosition(new THREE.Vector3()));
            this.cb.onSelectItem(it);
            return;
          }
        }
      }
    }

    // Loose-item click in room mode
    if (this.state.mode === 'room' && this.state.room) {
      const room = this.rooms.find(rm => rm.data.id === this.state.room!.id);
      if (room) {
        const itemHits = this.raycaster.intersectObjects(room.looseItemMarkers, true);
        if (itemHits.length) {
          const it = findItemInAncestors(itemHits[0].object);
          if (it) {
            this.spawnItemBurst(itemHits[0].object.getWorldPosition(new THREE.Vector3()));
            this.cb.onSelectItem(it);
            return;
          }
        }
      }
    }

    // Furniture click — recursive through procedural/glTF subtrees
    const groups = this.furniture.map(f => f.group);
    const fhits = this.raycaster.intersectObjects(groups, true);
    if (fhits.length) {
      const fid = findFurnitureIdInAncestors(fhits[0].object);
      if (fid != null) {
        const fmesh = this.furniture.find(f => f.data.id === fid);
        if (fmesh) { this.cb.onSelectFurniture(fmesh.data); return; }
      }
    }

    // Room click (floor or wall)
    const candidates: THREE.Object3D[] = [];
    for (const r of this.rooms) { candidates.push(r.floor); candidates.push(...r.walls); }
    const rhits = this.raycaster.intersectObjects(candidates, false);
    if (rhits.length) {
      const rid = rhits[0].object.userData['roomId'] as number;
      const room = this.rooms.find(r => r.data.id === rid);
      if (room) this.cb.onSelectRoom(room.data);
    }
  };

  private spawnItemBurst(pos: THREE.Vector3) {
    const count = 30;
    const geo = new THREE.BufferGeometry();
    const positions = new Float32Array(count * 3);
    const velocities: THREE.Vector3[] = [];
    for (let i = 0; i < count; i++) {
      positions[i*3] = pos.x; positions[i*3+1] = pos.y; positions[i*3+2] = pos.z;
      const v = new THREE.Vector3(
        (Math.random()-0.5),
        Math.random() * 1.0,
        (Math.random()-0.5)
      ).multiplyScalar(2);
      velocities.push(v);
    }
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const mat = new THREE.PointsMaterial({
      color: 0xff6fb3, size: 0.08, transparent: true, opacity: 1, depthWrite: false,
    });
    const pts = new THREE.Points(geo, mat);
    this.scene.add(pts);

    const start = performance.now();
    const tick = () => {
      const t = (performance.now() - start) / 800;
      if (t >= 1) { this.scene.remove(pts); geo.dispose(); mat.dispose(); return; }
      const arr = (geo.attributes['position'] as THREE.BufferAttribute).array as Float32Array;
      for (let i = 0; i < count; i++) {
        arr[i*3+0] = pos.x + velocities[i].x * t;
        arr[i*3+1] = pos.y + velocities[i].y * t - 1.6 * t * t;
        arr[i*3+2] = pos.z + velocities[i].z * t;
      }
      (geo.attributes['position'] as THREE.BufferAttribute).needsUpdate = true;
      mat.opacity = 1 - t;
      requestAnimationFrame(tick);
    };
    tick();
  }

  private animate = () => {
    this.rafId = requestAnimationFrame(this.animate);
    const dt = this.clock.getDelta();
    const t = this.clock.elapsedTime;

    this.controls.update();

    // Bob items
    const bob = (m: THREE.Mesh) => {
      if (!m.visible) return;
      const base = m.userData['basePos'] as THREE.Vector3;
      const off = m.userData['bobOffset'] as number;
      m.position.y = base.y + Math.sin(t * 2 + off) * 0.04;
      m.rotation.y += dt * 0.6;
    };
    for (const f of this.furniture) for (const m of f.itemMarkers) bob(m);
    for (const r of this.rooms) for (const m of r.looseItemMarkers) bob(m);

    // Slow star rotation
    if (this.starField) this.starField.rotation.y += dt * 0.01;

    this.renderer.render(this.scene, this.camera);
  };

  private onResize() {
    const w = this.host.clientWidth, h = this.host.clientHeight;
    if (!w || !h) return;
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h);
  }

  dispose() {
    cancelAnimationFrame(this.rafId);
    this.resizeObs?.disconnect();
    this.renderer.domElement.removeEventListener('pointermove', this.onPointerMove);
    this.renderer.domElement.removeEventListener('pointerdown', this.onPointerDown);
    this.renderer.domElement.removeEventListener('pointerup', this.onPointerUp);
    this.renderer.domElement.removeEventListener('click', this.onClick);
    this.renderer.dispose();
    this.controls.dispose();
    if (this.renderer.domElement.parentNode) this.renderer.domElement.parentNode.removeChild(this.renderer.domElement);
  }
}

function makeLabelSprite(text: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 512; canvas.height = 128;
  const ctx = canvas.getContext('2d')!;
  ctx.font = 'bold 64px ui-sans-serif, system-ui, sans-serif';
  ctx.fillStyle = 'rgba(255,255,255,0.95)';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.shadowColor = '#a78bfa';
  ctx.shadowBlur = 18;
  ctx.fillText(text, 256, 64);
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  const mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthWrite: false });
  const s = new THREE.Sprite(mat);
  s.scale.set(2.4, 0.6, 1);
  return s;
}
