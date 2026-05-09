import { Component, inject, signal, effect, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { Api } from '../../core/api';
import { AssetCategory, Container, Furniture, House, Item, Room } from '../../core/models';

@Component({
  selector: 'app-item-form',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="p-8 max-w-5xl fade-in">
      <a routerLink="/items" class="text-sm text-slate-400 hover:text-violet-300 inline-flex items-center gap-1 mb-4">
        <i class="pi pi-arrow-left"></i> Back to items
      </a>

      <div class="flex items-center justify-between mb-6">
        <h1 class="text-3xl font-semibold tracking-tight">
          {{ id() ? 'Edit item' : 'New item' }}
        </h1>
        @if (id()) {
          <button class="btn btn-danger" (click)="remove()">
            <i class="pi pi-trash"></i> Delete
          </button>
        }
      </div>

      <form (ngSubmit)="save()" class="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div class="lg:col-span-2 space-y-4">
          <div class="glass rounded-2xl p-5 space-y-4">
            <div>
              <label class="label">Name *</label>
              <input class="input" name="name" [(ngModel)]="model.name" required />
            </div>
            <div>
              <label class="label">Category</label>
              <select class="select" name="category" [(ngModel)]="model.category">
                <option [ngValue]="null">— None —</option>
                @for (category of assetCategories(); track category.id) {
                  <option [ngValue]="category.name">{{ category.name }}</option>
                }
              </select>
              @if (assetCategories().length === 0) {
                <div class="text-xs text-slate-500 mt-1">
                  Add categories in Settings to use this dropdown.
                </div>
              }
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Brand</label>
                <input class="input" name="brand" [(ngModel)]="model.brand" />
              </div>
              <div>
                <label class="label">Model</label>
                <input class="input" name="modelno" [(ngModel)]="model.model" />
              </div>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Serial number</label>
                <input class="input" name="sn" [(ngModel)]="model.serialNumber" />
              </div>
              <div>
                <label class="label">Quantity</label>
                <input class="input" type="number" name="qty" min="1"
                       [(ngModel)]="model.quantity" />
              </div>
            </div>
            <div>
              <label class="label">Description</label>
              <textarea class="textarea" rows="3" name="desc" [(ngModel)]="model.description"></textarea>
            </div>
          </div>

          <div class="glass rounded-2xl p-5 space-y-4">
            <h3 class="font-medium flex items-center gap-2"><i class="pi pi-dollar text-violet-300"></i> Value & dates</h3>
            <div class="grid grid-cols-3 gap-3">
              <div>
                <label class="label">Value</label>
                <input class="input" type="number" name="value" step="0.01"
                       [(ngModel)]="model.value" />
              </div>
              <div>
                <label class="label">Purchase date</label>
                <input class="input" type="date" name="pdate"
                       [(ngModel)]="model.purchaseDate" />
              </div>
              <div>
                <label class="label">Warranty until</label>
                <input class="input" type="date" name="wdate"
                       [(ngModel)]="model.warrantyUntil" />
              </div>
            </div>
          </div>

          <div class="glass rounded-2xl p-5 space-y-4">
            <h3 class="font-medium flex items-center gap-2"><i class="pi pi-tag text-violet-300"></i> Tags & notes</h3>
            <div>
              <label class="label">Tags (comma-separated)</label>
              <input class="input" name="tags" [(ngModel)]="tagsRaw" />
              @if (tagPreview().length) {
                <div class="flex gap-1 flex-wrap mt-2">
                  @for (t of tagPreview(); track t) { <span class="tag">{{ t }}</span> }
                </div>
              }
            </div>
            <div>
              <label class="label">Notes</label>
              <textarea class="textarea" rows="3" name="notes" [(ngModel)]="model.notes"></textarea>
            </div>
          </div>
        </div>

        <div class="space-y-4">
          <div class="glass rounded-2xl p-5">
            <h3 class="font-medium flex items-center gap-2 mb-4"><i class="pi pi-map-marker text-violet-300"></i> Location</h3>
            <div class="space-y-3">
              <div>
                <label class="label">Room</label>
                <select class="select" name="room"
                        [ngModel]="selectedRoomId()"
                        (ngModelChange)="onRoomChange($event)">
                  <option [ngValue]="null">— None —</option>
                  @for (r of rooms(); track r.id) {
                    <option [ngValue]="r.id">{{ r.name }}</option>
                  }
                </select>
              </div>
              <div>
                <label class="label">Furniture</label>
                <select class="select" name="furn"
                        [ngModel]="selectedFurnitureId()"
                        (ngModelChange)="onFurnitureChange($event)"
                        [disabled]="selectedRoomId() == null">
                  <option [ngValue]="null">— None —</option>
                  @for (f of furnitureInRoom(); track f.id) {
                    <option [ngValue]="f.id">{{ f.name }}</option>
                  }
                </select>
              </div>
              <div>
                <label class="label">Container (drawer/shelf)</label>
                <select class="select" name="cont"
                        [(ngModel)]="model.containerId"
                        [disabled]="selectedFurnitureId() == null">
                  <option [ngValue]="null">— None —</option>
                  @for (c of containers(); track c.id) {
                    <option [ngValue]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>
            </div>
          </div>

          <div class="glass rounded-2xl p-5">
            <h3 class="font-medium flex items-center gap-2 mb-4">
              <i class="pi pi-box text-violet-300"></i> 3D model
            </h3>
            @if (id()) {
              <input #modelFile type="file" class="hidden" accept=".glb,.gltf,model/gltf-binary,model/gltf+json"
                     (change)="onUploadModel(modelFile)" />
              @if (current()?.modelUrl) {
                <div class="px-3 py-2 rounded-lg border border-violet-400/30 bg-violet-500/10 flex items-center gap-2">
                  <i class="pi pi-check-circle text-violet-300"></i>
                  <span class="text-sm flex-1">Model uploaded</span>
                  <button type="button" class="btn btn-ghost !py-1 !px-2 text-xs" (click)="modelFile.click()" [disabled]="uploadingModel()">
                    <i class="pi pi-refresh"></i>
                  </button>
                  <button type="button" class="btn btn-danger !py-1 !px-2 text-xs" (click)="removeModel()">
                    <i class="pi pi-trash"></i>
                  </button>
                </div>
              } @else {
                <button type="button" class="btn btn-ghost w-full justify-center" (click)="modelFile.click()" [disabled]="uploadingModel()">
                  @if (uploadingModel()) { <i class="pi pi-spin pi-spinner"></i> Uploading… }
                  @else { <i class="pi pi-upload"></i> Upload .glb or .gltf }
                </button>
              }
              @if (modelError()) {
                <div class="text-red-300 text-xs bg-red-500/10 border border-red-500/30 rounded px-2 py-1.5 mt-2">
                  {{ modelError() }}
                </div>
              }
              <p class="text-xs text-slate-400 mt-2">
                The 3D planner will render this model floating where the item lives.
              </p>
            } @else {
              <p class="text-xs text-slate-400">Save the item first to upload a model.</p>
            }
          </div>

          <div class="glass rounded-2xl p-5">
            <h3 class="font-medium flex items-center gap-2 mb-4"><i class="pi pi-image text-violet-300"></i> Photos</h3>
            @if (id()) {
              <input #file type="file" class="hidden" accept="image/*" (change)="onUpload(file)" />
              <button type="button" class="btn btn-ghost w-full justify-center" (click)="file.click()" [disabled]="uploading()">
                @if (uploading()) { <i class="pi pi-spin pi-spinner"></i> Uploading… }
                @else { <i class="pi pi-upload"></i> Upload photo }
              </button>
              @if (current()?.photos?.length) {
                <div class="grid grid-cols-3 gap-2 mt-3">
                  @for (p of current()!.photos; track p.id) {
                    <div class="relative group aspect-square rounded-lg overflow-hidden bg-white/5">
                      <img [src]="api.photoUrl(p)" class="w-full h-full object-cover" />
                      <button type="button" (click)="removePhoto(p.id)"
                              class="absolute top-1 right-1 opacity-0 group-hover:opacity-100 transition w-6 h-6 grid place-items-center rounded-full bg-black/60 hover:bg-red-500/70">
                        <i class="pi pi-times text-xs"></i>
                      </button>
                    </div>
                  }
                </div>
              }
            } @else {
              <p class="text-xs text-slate-400">Save the item first to upload photos.</p>
            }
          </div>

          <div class="flex gap-2">
            <a routerLink="/items" class="btn btn-ghost flex-1 justify-center">Cancel</a>
            <button class="btn btn-primary flex-1 justify-center" type="submit" [disabled]="saving()">
              @if (saving()) { <i class="pi pi-spin pi-spinner"></i> }
              @else { <i class="pi pi-check"></i> }
              Save
            </button>
          </div>
          @if (error()) {
            <div class="text-red-300 text-sm bg-red-500/10 border border-red-500/30 rounded-lg px-3 py-2">
              {{ error() }}
            </div>
          }
        </div>
      </form>
    </div>
  `,
})
export class ItemFormComponent {
  protected api = inject(Api);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  id = signal<number | null>(null);
  current = signal<Item | null>(null);
  saving = signal(false);
  uploading = signal(false);
  uploadingModel = signal(false);
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
    this.api.listAssetCategories().subscribe(categories => this.assetCategories.set(categories));

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
    const op = this.id() ? this.api.updateItem(this.id()!, input) : this.api.createItem(input);
    op.subscribe({
      next: r => {
        this.saving.set(false);
        if (!this.id()) this.router.navigate(['/items', r.id]);
        else this.current.set(r);
      },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.id()) return;
    if (!confirm('Delete this item permanently?')) return;
    this.api.deleteItem(this.id()!).subscribe(() => this.router.navigate(['/items']));
  }

  onUpload(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file || !this.id()) return;
    this.uploading.set(true);
    this.api.uploadItemPhoto(this.id()!, file).subscribe({
      next: () => {
        this.uploading.set(false);
        input.value = '';
        this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
      },
      error: () => { this.uploading.set(false); },
    });
  }

  removePhoto(id: number) {
    this.api.deletePhoto(id).subscribe(() => {
      this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
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
      this.api.getItem(this.id()!).subscribe(i => this.current.set(i));
    });
  }
}
