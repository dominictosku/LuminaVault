import { Component, inject, signal, effect, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { InventoryApi } from '../../core/data-access/inventory-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import { AssetCategory, Container, Furniture, House, Item, Room } from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-item-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './item-form.html',
  styleUrl: './item-form.scss'
})
export class ItemFormComponent {
  protected api = inject(InventoryApi);
  private settingsApi = inject(SettingsApi);
  private confirmDialog = inject(ConfirmDialogService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toast = inject(ToastService);

  id = signal<number | null>(null);
  current = signal<Item | null>(null);
  saving = signal(false);
  uploading = signal(false);
  uploadingModel = signal(false);
  uploadingAttachment = signal(false);
  error = signal<string | null>(null);
  modelError = signal<string | null>(null);

  rooms = signal<Room[]>([]);
  furniture = signal<Furniture[]>([]);
  containers = signal<Container[]>([]);
  assetCategories = signal<AssetCategory[]>([]);

  selectedRoomId = signal<number | null>(null);
  selectedFurnitureId = signal<number | null>(null);

  furnitureInRoom = computed(() => {
    const id = this.selectedRoomId();
    return id == null ? [] : this.furniture().filter(f => f.roomId === id);
  });

  model = {
    name: '', description: '' as string | null,
    category: null as string | null,
    brand: '' as string | null, model: '' as string | null,
    serialNumber: '' as string | null,
    value: null as number | null,
    purchaseDate: null as string | null,
    warrantyUntil: null as string | null,
    quantity: 1,
    notes: '' as string | null,
    furnitureId: null as number | null,
    containerId: null as number | null,
  };
  tagsRaw = '';
  tagPreview = computed(() =>
    this.tagsRaw.split(',').map(t => t.trim()).filter(t => t.length > 0));

  constructor() {
    this.settingsApi.listAssetCategories().subscribe(categories => this.assetCategories.set(categories));

    this.api.listHouses().pipe(
      switchMap(houses => {
        if (!houses.length) return of({ rooms: [] as Room[], furniture: [] as Furniture[] });
        return forkJoin(houses.map(h => this.api.listRooms(h.id))).pipe(
          switchMap(roomLists => {
            const rooms = roomLists.flat();
            if (!rooms.length) return of({ rooms, furniture: [] as Furniture[] });
            return forkJoin(rooms.map(r => this.api.listFurniture(r.id))).pipe(
              switchMap(furnLists => of({ rooms, furniture: furnLists.flat() }))
            );
          })
        );
      })
    ).subscribe(r => {
      this.rooms.set(r.rooms);
      this.furniture.set(r.furniture);
    });

    this.route.paramMap.subscribe(p => {
      const idStr = p.get('id');
      if (idStr) {
        const id = +idStr;
        this.id.set(id);
        this.api.getItem(id).subscribe(item => {
          this.current.set(item);
          this.model.name = item.name;
          this.model.category = item.category ?? null;
          this.model.description = item.description ?? null;
          this.model.brand = item.brand ?? null;
          this.model.model = item.model ?? null;
          this.model.serialNumber = item.serialNumber ?? null;
          this.model.value = item.value ?? null;
          this.model.purchaseDate = item.purchaseDate ? item.purchaseDate.substring(0,10) : null;
          this.model.warrantyUntil = item.warrantyUntil ? item.warrantyUntil.substring(0,10) : null;
          this.model.quantity = item.quantity;
          this.model.notes = item.notes ?? null;
          this.model.furnitureId = item.furnitureId ?? null;
          this.model.containerId = item.containerId ?? null;
          this.tagsRaw = item.tags.join(', ');
          this.selectedRoomId.set(item.roomId ?? null);
          this.selectedFurnitureId.set(item.furnitureId ?? null);
          this.refreshContainers();
        });
      }
    });
  }

  onRoomChange(roomId: number | null) {
    this.selectedRoomId.set(roomId);
    this.selectedFurnitureId.set(null);
    this.model.furnitureId = null;
    this.model.containerId = null;
    this.containers.set([]);
  }

  onFurnitureChange(furnitureId: number | null) {
    this.selectedFurnitureId.set(furnitureId);
    this.model.furnitureId = furnitureId;
    this.model.containerId = null;
    this.refreshContainers();
  }

  refreshContainers() {
    const fid = this.selectedFurnitureId();
    if (!fid) { this.containers.set([]); return; }
    this.api.listContainers(fid).subscribe(c => this.containers.set(c));
  }

  save() {
    if (!this.model.name.trim()) { this.error.set('Name required.'); return; }
    this.error.set(null);
    this.saving.set(true);
    const input = {
      ...this.model,
      tags: this.tagPreview(),
      roomId: this.selectedRoomId(),
      purchaseDate: this.model.purchaseDate ? new Date(this.model.purchaseDate).toISOString() : null,
      warrantyUntil: this.model.warrantyUntil ? new Date(this.model.warrantyUntil).toISOString() : null,
    };
    const isUpdate = this.id() != null;
    const op = isUpdate ? this.api.updateItem(this.id()!, input) : this.api.createItem(input);
    op.subscribe({
      next: r => {
        this.saving.set(false);
        if (!isUpdate) {
          this.toast.success('Asset created.');
          this.router.navigate(['/items', r.id]);
        } else {
          this.current.set(r);
          this.toast.success('Asset updated.');
        }
      },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async remove() {
    if (!this.id()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete asset?',
      message: 'This asset will be permanently deleted.',
      detail: 'Photos and the attached 3D model are removed with it.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteItem(this.id()!).subscribe(() => {
      this.toast.success('Asset deleted.');
      this.router.navigate(['/items']);
    });
  }

  onUpload(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file || !this.id()) return;
    this.uploading.set(true);
    this.api.uploadItemPhoto(this.id()!, file).subscribe({
      next: () => {
        this.uploading.set(false);
        input.value = '';
        this.toast.success('Photo uploaded.');
        this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
      },
      error: () => { this.uploading.set(false); },
    });
  }

  removePhoto(id: number) {
    this.api.deletePhoto(id).subscribe(() => {
      this.toast.success('Photo removed.');
      this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
    });
  }

  onUploadAttachment(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file || !this.id()) return;
    this.uploadingAttachment.set(true);
    this.api.uploadItemAttachment(this.id()!, file).subscribe({
      next: () => {
        this.uploadingAttachment.set(false);
        input.value = '';
        this.toast.success('Document uploaded.');
        this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
      },
      error: e => {
        this.uploadingAttachment.set(false);
        input.value = '';
        this.error.set(e?.error?.error ?? 'Upload failed.');
      },
    });
  }

  removeAttachment(id: number) {
    this.api.deleteAttachment(id).subscribe(() => {
      this.toast.success('Document removed.');
      if (this.id()) this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
    });
  }

  onUploadModel(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file || !this.id()) return;
    this.modelError.set(null);
    this.uploadingModel.set(true);
    this.api.uploadItemModel(this.id()!, file).subscribe({
      next: () => {
        this.uploadingModel.set(false);
        input.value = '';
        this.toast.success('3D model uploaded.');
        this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
      },
      error: e => {
        this.uploadingModel.set(false);
        input.value = '';
        this.modelError.set(e?.error?.error ?? 'Upload failed.');
      },
    });
  }

  removeModel() {
    if (!this.id()) return;
    this.api.deleteItemModel(this.id()!).subscribe(() => {
      this.toast.success('3D model removed.');
      this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
    });
  }
}
