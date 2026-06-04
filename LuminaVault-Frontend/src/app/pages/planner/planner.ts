import {
  AfterViewInit, Component, ElementRef, OnDestroy, ViewChild,
  inject, signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { InventoryApi } from '../../core/data-access/inventory-api';
import { Furniture, Item, Room } from '../../core/models';
import { AuthService } from '../../core/auth.service';
import { ProtectedMediaSrcDirective } from '../../shared/protected-media-src.directive';
import { PlannerScene, PlannerState } from './scene';

@Component({
  selector: 'app-planner',
  imports: [RouterLink, ProtectedMediaSrcDirective],
  templateUrl: './planner.html',
  styleUrl: './planner.scss'
})
export class PlannerComponent implements AfterViewInit, OnDestroy {
  protected api = inject(InventoryApi);
  private auth = inject(AuthService);
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
      authToken: () => this.auth.token(),
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
            return forkJoin([furn$, this.api.listAllItems()]).pipe(
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
  looseItemsInRoom() {
    const id = this.state().room?.id;
    return id == null ? [] : this.items().filter(i => i.furnitureId == null && i.roomId === id);
  }
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
