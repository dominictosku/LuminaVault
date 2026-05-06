import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, of, switchMap } from 'rxjs';
import { Api } from '../../core/api';
import { Container, FURNITURE_KINDS, Furniture, FurnitureKind, House, Room } from '../../core/models';

@Component({
  selector: 'app-rooms',
  imports: [FormsModule],
  template: `
    <div class="p-8 fade-in">
      <div class="flex items-center justify-between mb-6">
        <div>
          <h1 class="text-3xl font-semibold tracking-tight">Rooms & furniture</h1>
          <p class="text-slate-400 text-sm mt-1">Build the structure your items live in.</p>
        </div>
      </div>

      @if (!house()) {
        <div class="glass rounded-2xl p-8 max-w-md">
          <h2 class="font-medium mb-3">Name your home</h2>
          <p class="text-sm text-slate-400 mb-4">A single house holds all your rooms.</p>
          <div class="flex gap-2">
            <input class="input" placeholder="My apartment" [(ngModel)]="newHouseName" name="hn" />
            <button class="btn btn-primary" (click)="createHouse()" [disabled]="!newHouseName.trim()">
              <i class="pi pi-check"></i> Create
            </button>
          </div>
        </div>
      } @else {
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div class="glass rounded-2xl p-5">
            <h3 class="font-medium mb-3 flex items-center gap-2">
              <i class="pi pi-home text-violet-300"></i> Rooms
            </h3>
            <div class="space-y-2 mb-4">
              @for (r of rooms(); track r.id) {
                <button (click)="selectRoom(r)"
                        [class.bg-violet-500\\/20]="selectedRoom()?.id === r.id"
                        [class.border-violet-400\\/40]="selectedRoom()?.id === r.id"
                        class="w-full text-left px-3 py-2.5 rounded-lg border border-white/10 hover:bg-white/5 transition flex items-center gap-2">
                  <div class="w-3 h-3 rounded-full" [style.background]="r.color"></div>
                  <span>{{ r.name }}</span>
                </button>
              }
              @if (!rooms().length) {
                <div class="text-sm text-slate-500">No rooms yet.</div>
              }
            </div>

            <div class="space-y-2 border-t border-white/10 pt-3">
              <input class="input" placeholder="New room name" name="rn" [(ngModel)]="newRoomName" />
              <input class="input h-10" type="color" name="rc" [(ngModel)]="newRoomColor" />
              <button class="btn btn-primary w-full justify-center" (click)="createRoom()" [disabled]="!newRoomName.trim()">
                <i class="pi pi-plus"></i> Add room
              </button>
            </div>
          </div>

          <div class="lg:col-span-2 space-y-4">
            @if (!selectedRoom()) {
              <div class="glass rounded-2xl p-10 text-center text-slate-400">
                Select a room to manage its furniture.
              </div>
            } @else {
              <div class="glass rounded-2xl p-5">
                <div class="flex items-center justify-between mb-4">
                  <h3 class="font-medium flex items-center gap-2">
                    <i class="pi pi-th-large text-violet-300"></i> Furniture in {{ selectedRoom()!.name }}
                  </h3>
                  <button class="btn btn-danger" (click)="deleteRoom()">
                    <i class="pi pi-trash"></i> Delete room
                  </button>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
                  @for (f of furniture(); track f.id) {
                    <div (click)="selectFurniture(f)"
                         [class.border-violet-400\\/40]="selectedFurniture()?.id === f.id"
                         [class.bg-violet-500\\/10]="selectedFurniture()?.id === f.id"
                         class="text-left px-3 py-2.5 rounded-lg border border-white/10 hover:bg-white/5 transition cursor-pointer flex items-center gap-2">
                      <div class="flex-1 min-w-0">
                        <div class="font-medium text-sm truncate">{{ f.name }}</div>
                        <div class="text-xs text-slate-400">{{ f.kind }} · {{ f.itemCount }} items · {{ f.containerCount }} containers</div>
                      </div>
                      <button class="btn btn-danger !py-1 !px-2 shrink-0"
                              (click)="deleteFurniture(f.id, $event)">
                        <i class="pi pi-trash text-xs"></i>
                      </button>
                    </div>
                  }
                  @if (!furniture().length) {
                    <div class="text-sm text-slate-500">No furniture yet.</div>
                  }
                </div>

                <div class="mt-4 pt-4 border-t border-white/10 grid grid-cols-2 gap-2">
                  <input class="input" placeholder="Furniture name" name="fn" [(ngModel)]="newFurniture.name" />
                  <select class="select" name="fk" [(ngModel)]="newFurniture.kind">
                    @for (k of kinds; track k) { <option [ngValue]="k">{{ k }}</option> }
                  </select>
                  <button class="btn btn-primary col-span-2 justify-center" (click)="addFurniture()" [disabled]="!newFurniture.name.trim()">
                    <i class="pi pi-plus"></i> Add furniture
                  </button>
                </div>
              </div>

              @if (selectedFurniture()) {
                <div class="glass rounded-2xl p-5">
                  <div class="flex items-center gap-2 mb-4">
                    <i class="pi pi-folder text-violet-300 shrink-0"></i>
                    @if (renamingFurniture()) {
                      <input class="input flex-1" name="frename" [(ngModel)]="editFurnitureName"
                             (keydown.enter)="saveRename()" (keydown.escape)="renamingFurniture.set(false)" />
                      <button class="btn btn-primary !py-1 !px-2" (click)="saveRename()">
                        <i class="pi pi-check text-xs"></i>
                      </button>
                      <button class="btn btn-ghost !py-1 !px-2" (click)="renamingFurniture.set(false)">
                        <i class="pi pi-times text-xs"></i>
                      </button>
                    } @else {
                      <h3 class="font-medium flex-1">Containers in {{ selectedFurniture()!.name }}</h3>
                      <button class="btn btn-ghost !py-1 !px-2" (click)="startRename()">
                        <i class="pi pi-pencil text-xs"></i>
                      </button>
                    }
                  </div>
                  <div class="space-y-2">
                    @for (c of containers(); track c.id) {
                      <div class="flex items-center justify-between px-3 py-2 rounded-lg border border-white/10">
                        <div>
                          <div class="text-sm">{{ c.name }}</div>
                          <div class="text-xs text-slate-500">{{ c.itemCount }} items</div>
                        </div>
                        <button class="btn btn-danger" (click)="deleteContainer(c.id)">
                          <i class="pi pi-trash"></i>
                        </button>
                      </div>
                    }
                    @if (!containers().length) {
                      <div class="text-sm text-slate-500">e.g. "Top drawer", "Left shelf"…</div>
                    }
                  </div>
                  <div class="mt-4 pt-4 border-t border-white/10 flex gap-2">
                    <input class="input" placeholder="Container name (e.g. Top drawer)" [(ngModel)]="newContainerName" name="cn" />
                    <button class="btn btn-primary" (click)="addContainer()" [disabled]="!newContainerName.trim()">
                      <i class="pi pi-plus"></i>
                    </button>
                  </div>
                </div>
              }
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class RoomsComponent {
  private api = inject(Api);

  kinds = FURNITURE_KINDS;
  house = signal<House | null>(null);
  rooms = signal<Room[]>([]);
  furniture = signal<Furniture[]>([]);
  containers = signal<Container[]>([]);
  selectedRoom = signal<Room | null>(null);
  selectedFurniture = signal<Furniture | null>(null);

  newHouseName = '';
  newRoomName = '';
  newRoomColor = '#7c3aed';
  newFurniture = { name: '', kind: 'Cabinet' as FurnitureKind };
  newContainerName = '';
  renamingFurniture = signal(false);
  editFurnitureName = '';

  constructor() { this.refresh(); }

  refresh() {
    this.api.listHouses().pipe(
      switchMap(houses => {
        if (!houses.length) return of({ house: null as House | null, rooms: [] as Room[] });
        const h = houses[0];
        return this.api.listRooms(h.id).pipe(
          switchMap(rooms => of({ house: h, rooms }))
        );
      })
    ).subscribe(r => {
      this.house.set(r.house);
      this.rooms.set(r.rooms);
    });
  }

  createHouse() {
    this.api.createHouse(this.newHouseName.trim()).subscribe(() => {
      this.newHouseName = '';
      this.refresh();
    });
  }

  selectRoom(r: Room) {
    this.selectedRoom.set(r);
    this.selectedFurniture.set(null);
    this.containers.set([]);
    this.api.listFurniture(r.id).subscribe(f => this.furniture.set(f));
  }

  deleteRoom() {
    const r = this.selectedRoom();
    if (!r || !confirm(`Delete room "${r.name}" and all its furniture?`)) return;
    this.api.deleteRoom(r.id).subscribe(() => {
      this.selectedRoom.set(null);
      this.refresh();
    });
  }

  createRoom() {
    if (!this.house()) return;
    const placement = this.suggestRoomPlacement();
    this.api.createRoom(this.house()!.id, {
      name: this.newRoomName.trim(),
      color: this.newRoomColor,
      ...placement,
      height: 2.6,
    }).subscribe(() => {
      this.newRoomName = '';
      this.api.listRooms(this.house()!.id).subscribe(r => this.rooms.set(r));
    });
  }

  private suggestRoomPlacement() {
    const rs = this.rooms();
    if (!rs.length) return { x: 0, z: 0, width: 4, depth: 4 };
    const right = Math.max(...rs.map(r => r.x + r.width));
    return { x: right + 0.5, z: 0, width: 4, depth: 4 };
  }

  selectFurniture(f: Furniture) {
    this.selectedFurniture.set(f);
    this.renamingFurniture.set(false);
    this.api.listContainers(f.id).subscribe(c => this.containers.set(c));
  }

  deleteFurniture(id: number, event: Event) {
    event.stopPropagation();
    if (!confirm('Delete this furniture and all its containers?')) return;
    this.api.deleteFurniture(id).subscribe(() => {
      if (this.selectedFurniture()?.id === id) {
        this.selectedFurniture.set(null);
        this.containers.set([]);
      }
      const r = this.selectedRoom();
      if (r) this.api.listFurniture(r.id).subscribe(f => this.furniture.set(f));
    });
  }

  startRename() {
    this.editFurnitureName = this.selectedFurniture()!.name;
    this.renamingFurniture.set(true);
  }

  saveRename() {
    const f = this.selectedFurniture();
    if (!f || !this.editFurnitureName.trim()) return;
    this.api.updateFurniture(f.id, { ...f, name: this.editFurnitureName.trim() }).subscribe(updated => {
      this.renamingFurniture.set(false);
      const r = this.selectedRoom();
      if (r) this.api.listFurniture(r.id).subscribe(list => {
        this.furniture.set(list);
        this.selectedFurniture.set(list.find(x => x.id === updated.id) ?? null);
      });
    });
  }

  addFurniture() {
    const r = this.selectedRoom();
    if (!r) return;
    this.api.createFurniture(r.id, {
      name: this.newFurniture.name.trim(),
      kind: this.newFurniture.kind,
      x: r.width / 2 - 0.4, y: 0, z: r.depth / 2 - 0.25,
      width: 0.8, depth: 0.5, height: 1.2, rotationY: 0,
    }).subscribe(() => {
      this.newFurniture.name = '';
      this.api.listFurniture(r.id).subscribe(f => this.furniture.set(f));
    });
  }

  addContainer() {
    const f = this.selectedFurniture();
    if (!f) return;
    this.api.createContainer(f.id, this.newContainerName.trim()).subscribe(() => {
      this.newContainerName = '';
      this.api.listContainers(f.id).subscribe(c => this.containers.set(c));
    });
  }

  deleteContainer(id: number) {
    if (!confirm('Delete this container?')) return;
    this.api.deleteContainer(id).subscribe(() => {
      const f = this.selectedFurniture();
      if (f) this.api.listContainers(f.id).subscribe(c => this.containers.set(c));
    });
  }
}
