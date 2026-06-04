import * as THREE from 'three';
import { starFieldPositions } from './planner-layout';

export interface PlannerEnvironment {
  /// The only environment object the animation loop needs a handle on (slow spin).
  starField: THREE.Points;
}

/// Builds the fixed stage the planner renders into — lighting rig, ground plane,
/// reference grid, and the star dome — and adds it all to the scene. Pulled out of
/// PlannerScene.init() so scene construction reads as "set up renderer/camera/controls,
/// then drop in the environment" rather than 60 lines of inline mesh wiring.
export function createEnvironment(scene: THREE.Scene): PlannerEnvironment {
  // Lights: warm ambient + a key directional that casts shadows, plus two coloured
  // point lights for the glassmorphism rim glow.
  const ambient = new THREE.AmbientLight(0xb8a8ff, 0.45);
  scene.add(ambient);

  const key = new THREE.DirectionalLight(0xffffff, 1.0);
  key.position.set(8, 18, 6);
  key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  key.shadow.camera.left = -30;
  key.shadow.camera.right = 30;
  key.shadow.camera.top = 30;
  key.shadow.camera.bottom = -30;
  scene.add(key);

  const fill = new THREE.PointLight(0xff5fa3, 1.4, 30);
  fill.position.set(-6, 6, -4);
  scene.add(fill);

  const rim = new THREE.PointLight(0x7c3aed, 1.2, 30);
  rim.position.set(8, 5, -8);
  scene.add(rim);

  // Ground plane + grid.
  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(120, 120),
    new THREE.MeshStandardMaterial({ color: 0x0e0c1a, roughness: 0.95, metalness: 0.0 }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.position.y = -0.001;
  ground.receiveShadow = true;
  scene.add(ground);

  const grid = new THREE.GridHelper(120, 120, 0x2a2540, 0x18152a);
  (grid.material as THREE.Material).transparent = true;
  (grid.material as THREE.Material).opacity = 0.5;
  scene.add(grid);

  // Star dome.
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(starFieldPositions(1500), 3));
  const mat = new THREE.PointsMaterial({
    size: 0.18, color: 0xc4b5fd, transparent: true, opacity: 0.7,
    sizeAttenuation: true, depthWrite: false,
  });
  const starField = new THREE.Points(geo, mat);
  scene.add(starField);

  return { starField };
}
