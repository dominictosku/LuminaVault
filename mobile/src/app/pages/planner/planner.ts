import {
  AfterViewInit, Component, ElementRef, OnDestroy, ViewChild,
  inject, signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { Api } from '../../core/api';
import { Furniture, Item, Room } from '../../core/models';
import { PlannerScene, PlannerState } from './scene';

@Component({
  selector: 'app-planner',
  imports: [RouterLink],
  template: `
    <div class="relative h-screen w-full">
      <div #host class="absolute inset-0"></div>

      <!-- Top bar -->
      <div class="absolute top-0 left-0 right-0 p-5 flex items-start justify-between pointer-events-none">
        <div class="glass rounded-2xl px-4 py-3 pointer-events-auto fade-in">
          <div class="flex items-center gap-2 text-sm">
            <button class="hover:text-violet-300 transition" (click)="goOverview()" [class.text-violet-300]="state().mode==='overview'">
              <i class="pi pi-globe"></i> Overview
            </button>
            @if (state().room) {
              <span class="text-slate-500">›</span>
              <button class="hover:text-violet-300 transition" (click)="goRoom(state().room!)" [class.text-violet-300]="state().mode==='room'">
                {{ state().room!.name }}
              </button>
            }
            @if (state().furniture) {
              <span class="text-slate-500">›</span>
              <span class="text-violet-300">{{ state().furniture!.name }}</span>
            }
          </div>
        </div>

        <div class="glass rounded-2xl px-4 py-3 pointer-events-auto text-xs text-slate-300 max-w-xs fade-in">
          <div class="flex items-center gap-2 font-medium text-slate-200 mb-1">
            <i class="pi pi-info-circle text-violet-300"></i> Controls
          </div>
          <div>Drag to orbit · Scroll to zoom</div>
          <div>Click a room → enter it · click furniture → see items inside</div>
          <div><span class="text-violet-300">Shift+drag</span> furniture to move it</div>
        </div>
      </div>

      <!-- Side panel -->
      <div class="absolute top-24 left-5 bottom-5 w-80 glass rounded-2xl p-5 overflow-y-auto pointer-events-auto fade-in">
        @if (state().mode === 'overview') {
          <h2 class="font-semibold mb-4">Your home</h2>
          <p class="text-sm text-slate-400 mb-4">Click a room to enter, or pick one here.</p>
          <div class="space-y-2">
            @for (r of rooms(); track r.id) {
              <button class="w-full text-left px-3 py-2.5 rounded-lg border border-white/10 hover:bg-white/5 transition flex items-center gap-2" (click)="goRoom(r)">
                <div class="w-3 h-3 rounded-full" [style.background]="r.color"></div>
                <span class="flex-1">{{ r.name }}</span>
                <span class="text-xs text-slate-500">{{ countInRoom(r.id) }} items</span>
              </button>
            }
            @if (!rooms().length) {
              <div class="text-sm text-slate-500">
                No rooms yet. Add some on the
                <a routerLink="/rooms" class="text-violet-300 hover:underline">Rooms</a> page.
              </div>
            }
          </div>
        }

        @if (state().mode === 'room' && state().room) {
          <h2 class="font-semibold mb-1">{{ state().room!.name }}</h2>
          <p class="text-xs text-slate-400 mb-4">Click furniture in the scene to inspect it.</p>
          <div class="space-y-2">
            @for (f of furnitureInRoom(); track f.id) {
              <button class="w-full text-left px-3 py-2.5 rounded-lg border border-white/10 hover:bg-white/5 transition" (click)="goFurniture(f)">
                <div class="font-medium text-sm">{{ f.name }}</div>
                <div class="text-xs text-slate-400">{{ f.kind }} · {{ f.itemCount }} items</div>
              </button>
            }
            @if (!furnitureInRoom().length) {
              <div class="text-sm text-slate-500">
                No furniture in here. Add some on the
                <a routerLink="/rooms" class="text-violet-300 hover:underline">Rooms</a> page.
              </div>
            }
          </div>
        }

        @if (state().mode === 'furniture' && state().furniture) {
          <h2 class="font-semibold mb-1">{{ state().furniture!.name }}</h2>
          <p class="text-xs text-slate-400 mb-4">Items stored here. Hover an orb in the scene to preview.</p>
          <div class="space-y-2">
            @for (it of itemsInFurniture(); track it.id) {
              <button class="w-full text-left px-3 py-2.5 rounded-lg border border-white/10 hover:bg-white/5 transition flex items-center gap-3" (click)="openItem(it)">
                <div class="w-10 h-10 rounded-lg overflow-hidden bg-violet-500/10 grid place-items-center shrink-0">
                  @if (it.photos[0]) {
                    <img [src]="api.photoUrl(it.photos[0])" class="w-full h-full object-cover" />
                  } @else {
                    <i class="pi pi-box text-violet-300"></i>
                  }
                </div>
                <div class="min-w-0 flex-1">
                  <div class="font-medium text-sm truncate">{{ it.name }}</div>
                  <div class="text-xs text-slate-400 truncate">
                    {{ it.containerName || 'Loose' }}
                    @if (it.quantity > 1) { · ×{{ it.quantity }} }
                  </div>
                </div>
              </button>
            }
            @if (!itemsInFurniture().length) {
              <div class="text-sm text-slate-500">No items linked here yet.</div>
            }
          </div>
          <a [routerLink]="['/items/new']" [queryParams]="{ furniture: state().furniture!.id }"
             class="btn btn-primary w-full justify-center mt-4">
            <i class="pi pi-plus"></i> Add item here
          </a>
        }
      </div>

      <!-- Hover preview -->
      @if (hovered()) {
        <div class="absolute bottom-5 right-5 glass rounded-2xl p-4 w-72 pointer-events-none fade-in">
          <div class="flex items-center gap-3">
            <div class="w-12 h-12 rounded-lg overflow-hidden bg-violet-500/10 grid place-items-center shrink-0">
              @if (hovered()!.photos[0]) {
                <img [src]="api.photoUrl(hovered()!.photos[0])" class="w-full h-full object-cover" />
              } @else {
                <i class="pi pi-box text-violet-300"></i>
              }
            </div>
            <div class="min-w-0 flex-1">
              <div class="font-medium truncate">{{ hovered()!.name }}</div>
              <div class="text-xs text-slate-400 truncate">
                {{ hovered()!.brand || '' }}
                @if (hovered()!.containerName) { · {{ hovered()!.containerName }} }
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class PlannerComponent implements AfterViewInit, OnDestroy {
  protected api = inject(Api);
  private router = inject(Router);

  @ViewChild('host', { static: true }) host!: ElementRef<HTMLDivElement>;

  rooms = signal<Room[]>([]);
  furniture = signal<Furniture[]>([]);
  items = signal<Item[]>([]);
  state = signal<PlannerState>({ mode: 'overview' });
  hovered = signal<Item | null>(null);

  private engine: PlannerScene | null = null;

  ngAfterViewInit(): void {
    this.engine = new PlannerScene(this.host.nativeElement, {
      onSelectRoom: (r) => this.goRoom(r),
      onSelectFurniture: (f) => this.goFurniture(f),
      onSelectItem: (i) => this.openItem(i),
      onHoverItem: (i) => this.hovered.set(i),
      onMoveFurniture: (f, x, z) => this.persistFurniturePosition(f, x, z),
    });
    this.refresh();
  }

  ngOnDestroy(): void { this.engine?.dispose(); }

  refresh() {
    this.api.listHouses().pipe(
      switchMap(houses => {
        if (!houses.length) return of({ rooms: [] as Room[], furniture: [] as Furniture[], items: [] as Item[] });
        return forkJoin(houses.map(h => this.api.listRooms(h.id))).pipe(
          switchMap(roomLists => {
            const rooms = roomLists.flat();
            const furn$ = rooms.length ? forkJoin(rooms.map(r => this.api.listFurniture(r.id))) : of([] as Furniture[][]);
            return forkJoin([furn$, this.api.listItems()]).pipe(
              switchMap(([fLists, items]) => of({ rooms, furniture: (fLists as Furniture[][]).flat(), items }))
            );
          })
        );
      })
    ).subscribe(d => {
      this.rooms.set(d.rooms);
      this.furniture.set(d.furniture);
      this.items.set(d.items);
      this.engine?.setData(d.rooms, d.furniture, d.items);
    });
  }

  countInRoom(roomId: number) { return this.items().filter(i => i.roomId === roomId).length; }
  furnitureInRoom() { return this.furniture().filter(f => f.roomId === this.state().room?.id); }
  itemsInFurniture() { return this.items().filter(i => i.furnitureId === this.state().furniture?.id); }

  goOverview() {
    this.state.set({ mode: 'overview' });
    this.engine?.setMode(this.state());
  }
  goRoom(r: Room) {
    this.state.set({ mode: 'room', room: r });
    this.engine?.setMode(this.state());
  }
  goFurniture(f: Furniture) {
    const r = this.rooms().find(rr => rr.id === f.roomId);
    this.state.set({ mode: 'furniture', furniture: f, room: r });
    this.engine?.setMode(this.state());
  }
  openItem(i: Item) { this.router.navigate(['/items', i.id]); }

  private persistFurniturePosition(f: Furniture, x: number, z: number) {
    this.api.updateFurniture(f.id, {
      name: f.name, kind: f.kind,
      x, y: f.y, z,
      width: f.width, depth: f.depth, height: f.height,
      rotationY: f.rotationY,
    }).subscribe();
  }
}
