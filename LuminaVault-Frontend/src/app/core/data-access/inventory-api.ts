import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  Container,
  DocumentAttachment,
  Furniture,
  House,
  Item,
  ItemInput,
  ItemPhoto,
  Room,
  StatsSummary,
} from '../models';

@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private http = inject(HttpClient);

  listHouses() {
    return this.http.get<House[]>(`${API_BASE}/api/houses`);
  }

  createHouse(name: string, description?: string) {
    return this.http.post<House>(`${API_BASE}/api/houses`, { name, description });
  }

  updateHouse(id: number, name: string, description?: string) {
    return this.http.put<House>(`${API_BASE}/api/houses/${id}`, { name, description });
  }

  deleteHouse(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/houses/${id}`);
  }

  listRooms(houseId: number) {
    return this.http.get<Room[]>(`${API_BASE}/api/houses/${houseId}/rooms`);
  }

  createRoom(houseId: number, room: Omit<Room, 'id' | 'houseId'>) {
    return this.http.post<Room>(`${API_BASE}/api/houses/${houseId}/rooms`, room);
  }

  updateRoom(id: number, room: Omit<Room, 'id' | 'houseId'>) {
    return this.http.put<Room>(`${API_BASE}/api/rooms/${id}`, room);
  }

  deleteRoom(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/rooms/${id}`);
  }

  listFurniture(roomId: number) {
    return this.http.get<Furniture[]>(`${API_BASE}/api/rooms/${roomId}/furniture`);
  }

  createFurniture(
    roomId: number,
    furniture: Omit<Furniture, 'id' | 'roomId' | 'itemCount' | 'containerCount'>,
  ) {
    return this.http.post<Furniture>(`${API_BASE}/api/rooms/${roomId}/furniture`, furniture);
  }

  updateFurniture(
    id: number,
    furniture: Omit<Furniture, 'id' | 'roomId' | 'itemCount' | 'containerCount'>,
  ) {
    return this.http.put<Furniture>(`${API_BASE}/api/furniture/${id}`, furniture);
  }

  deleteFurniture(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/furniture/${id}`);
  }

  listContainers(furnitureId: number) {
    return this.http.get<Container[]>(`${API_BASE}/api/furniture/${furnitureId}/containers`);
  }

  createContainer(furnitureId: number, name: string, description?: string) {
    return this.http.post<Container>(`${API_BASE}/api/furniture/${furnitureId}/containers`, {
      name,
      description,
    });
  }

  updateContainer(id: number, name: string, description?: string) {
    return this.http.put<Container>(`${API_BASE}/api/containers/${id}`, { name, description });
  }

  deleteContainer(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/containers/${id}`);
  }

  listItems(opts: { q?: string; furnitureId?: number; containerId?: number; roomId?: number } = {}) {
    let params = new HttpParams();
    if (opts.q) params = params.set('q', opts.q);
    if (opts.furnitureId) params = params.set('furnitureId', opts.furnitureId);
    if (opts.containerId) params = params.set('containerId', opts.containerId);
    if (opts.roomId) params = params.set('roomId', opts.roomId);
    return this.http.get<Item[]>(`${API_BASE}/api/items`, { params });
  }

  getItem(id: number) {
    return this.http.get<Item>(`${API_BASE}/api/items/${id}`);
  }

  createItem(input: ItemInput) {
    return this.http.post<Item>(`${API_BASE}/api/items`, input);
  }

  updateItem(id: number, input: ItemInput) {
    return this.http.put<Item>(`${API_BASE}/api/items/${id}`, input);
  }

  deleteItem(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/items/${id}`);
  }

  uploadItemPhoto(itemId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<ItemPhoto>(`${API_BASE}/api/items/${itemId}/photos`, fd);
  }

  deletePhoto(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/photos/${id}`);
  }

  uploadItemModel(itemId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<{ modelUrl: string }>(`${API_BASE}/api/items/${itemId}/model`, fd);
  }

  deleteItemModel(itemId: number) {
    return this.http.delete<void>(`${API_BASE}/api/items/${itemId}/model`);
  }

  uploadItemAttachment(itemId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<DocumentAttachment>(`${API_BASE}/api/items/${itemId}/attachments`, fd);
  }

  stats() {
    return this.http.get<StatsSummary>(`${API_BASE}/api/items/stats/summary`);
  }

  modelUrl(item: { modelUrl?: string | null }) {
    return item.modelUrl ? `${API_BASE}${item.modelUrl}` : null;
  }

  photoUrl(photo: ItemPhoto) {
    return `${API_BASE}${photo.url}`;
  }
}
