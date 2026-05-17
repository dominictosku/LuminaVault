import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, of, switchMap } from 'rxjs';
import { InventoryApi } from '../../core/data-access/inventory-api';
import { Container, FURNITURE_KINDS, Furniture, FurnitureKind, House, Room } from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-rooms',
  imports: [FormsModule],
  templateUrl: './rooms.html',
  styleUrl: './rooms.scss'
})
export class RoomsComponent {
  private api = inject(InventoryApi);
  private confirmDialog = inject(ConfirmDialogService);
  private toast = inject(ToastService);

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
      this.toast.success('House created.');
      this.refresh();
    });
  }

  selectRoom(r: Room) {
    this.selectedRoom.set(r);
    this.selectedFurniture.set(null);
    this.containers.set([]);
    this.api.listFurniture(r.id).subscribe(f => this.furniture.set(f));
  }

  async deleteRoom() {
    const r = this.selectedRoom();
    if (!r) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete room?',
      message: `Delete "${r.name}" and all its furniture?`,
      detail: 'Containers inside that furniture are removed too.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteRoom(r.id).subscribe(() => {
      this.toast.success('Room deleted.');
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
      this.toast.success('Room created.');
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

  async deleteFurniture(id: number, event: Event) {
    event.stopPropagation();
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete furniture?',
      message: 'Delete this furniture and all its containers?',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteFurniture(id).subscribe(() => {
      this.toast.success('Furniture deleted.');
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
      this.toast.success('Furniture renamed.');
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
      this.toast.success('Furniture added.');
      this.api.listFurniture(r.id).subscribe(f => this.furniture.set(f));
    });
  }

  addContainer() {
    const f = this.selectedFurniture();
    if (!f) return;
    this.api.createContainer(f.id, this.newContainerName.trim()).subscribe(() => {
      this.newContainerName = '';
      this.toast.success('Container added.');
      this.api.listContainers(f.id).subscribe(c => this.containers.set(c));
    });
  }

  async deleteContainer(id: number) {
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete container?',
      message: 'This container will be removed.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteContainer(id).subscribe(() => {
      this.toast.success('Container deleted.');
      const f = this.selectedFurniture();
      if (f) this.api.listContainers(f.id).subscribe(c => this.containers.set(c));
    });
  }
}
